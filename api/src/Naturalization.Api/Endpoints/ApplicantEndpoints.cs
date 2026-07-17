using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Auth;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;
using Naturalization.Api.Dtos;
using Naturalization.Api.Services;

namespace Naturalization.Api.Endpoints;

public static class ApplicantEndpoints
{
    public static void MapApplicants(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/applicants")
            .WithTags("Applicants")
            .RequireAuthorization();

        g.MapGet("/", async (NaturalizationDbContext db, string? q, int page = 1, int pageSize = 15) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = db.Applicants.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                // Match against the real name columns, not the computed FullName —
                // a [NotMapped] property can't be translated to SQL.
                query = query.Where(a =>
                    EF.Functions.Like(a.FirstName, $"%{term}%") ||
                    EF.Functions.Like(a.MiddleName, $"%{term}%") ||
                    EF.Functions.Like(a.LastName, $"%{term}%") ||
                    EF.Functions.Like(a.AlienNumber, $"%{term}%"));
            }

            var total = await query.CountAsync();

            // Materialise the entities WITH their locality, then map in memory.
            // Projecting straight to ApplicantDto in SQL would make EF drop the
            // Include (the query no longer returns the entity type), leaving
            // a.Locality null and the city/state/ZIP blank on every row.
            var rows = await query
                .Include(a => a.Locality)
                .OrderBy(a => a.LastName)
                .ThenBy(a => a.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var items = rows.Select(ApplicantDto.From).ToList();

            return TypedResults.Ok(new Paged<ApplicantDto>(items, page, pageSize, total));
        })
        .WithName("ListApplicants")
        .WithSummary("List applicants, optionally filtered by name or A-Number.");

        g.MapGet("/{id:int}", async Task<Results<Ok<ApplicantDto>, NotFound>> (NaturalizationDbContext db, int id) =>
        {
            var a = await db.Applicants.AsNoTracking()
                .Include(x => x.Locality)
                .FirstOrDefaultAsync(x => x.Id == id);
            return a is null ? TypedResults.NotFound() : TypedResults.Ok(ApplicantDto.From(a));
        })
        .WithName("GetApplicant");

        /*
         * The record's own history: created, updated, withdrawn, restored, by whom
         * and when. This is what makes the soft delete visible — withdraw a record
         * and the trail keeps growing rather than vanishing.
         */
        g.MapGet("/{id:int}/history", async Task<Results<Ok<List<AuditEventDto>>, NotFound>> (
            NaturalizationDbContext db, int id) =>
        {
            // IgnoreQueryFilters: the history of a WITHDRAWN applicant is precisely
            // the history you most want to be able to read.
            var exists = await db.Applicants.IgnoreQueryFilters().AnyAsync(a => a.Id == id);
            if (!exists) return TypedResults.NotFound();

            var events = await db.AuditEvents.AsNoTracking()
                .Where(e => e.EntityType == nameof(Applicant) && e.EntityId == id)
                .OrderByDescending(e => e.OccurredAt)
                .Select(e => AuditEventDto.From(e))
                .ToListAsync();

            return TypedResults.Ok(events);
        })
        .WithName("GetApplicantHistory")
        .WithSummary("Audit trail for one applicant record. Readable even once withdrawn.");

        g.MapPost("/", async Task<Results<Created<ApplicantDto>, ValidationProblem, Conflict<string>>> (
            NaturalizationDbContext db, ClaimsPrincipal user, ApplicantInput input) =>
        {
            if (Validate(input) is { Count: > 0 } errors) return TypedResults.ValidationProblem(errors);

            // Residence is a real FK now: a LocalityId that doesn't exist would trip
            // the constraint and surface as a raw 500. Turn it into a field error.
            if (await UnknownLocality(db, input.LocalityId) is { } localityError)
                return TypedResults.ValidationProblem(localityError);

            /*
             * IgnoreQueryFilters is doing real work here.
             *
             * The unique index on AlienNumber is NOT filtered by IsDeleted — an
             * A-Number identifies a person, and withdrawing their record does not
             * release it. But the default query filter HIDES withdrawn applicants,
             * so a plain `AnyAsync` would report "no collision", the insert would
             * hit the index anyway, and the caller would get a raw
             * DbUpdateException 500 instead of an answer they can act on.
             */
            var clash = await db.Applicants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.AlienNumber == input.AlienNumber);

            if (clash is { IsDeleted: true })
            {
                return TypedResults.Conflict(
                    $"A-Number {input.AlienNumber} belongs to withdrawn applicant #{clash.Id}. "
                        + $"Restore that record instead of creating a duplicate: "
                        + $"POST /api/applicants/{clash.Id}/restore");
            }

            if (clash is not null)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["alienNumber"] = [$"An applicant with A-Number {input.AlienNumber} already exists."]
                });
            }

            var a = new Applicant
            {
                AlienNumber = input.AlienNumber,
                NaturalizationNumber = (input.NaturalizationNumber ?? "").Trim(),
                PetitionNumber = (input.PetitionNumber ?? "").Trim(),
                FirstName = input.FirstName,
                MiddleName = NormalizeMiddle(input.MiddleName),
                LastName = input.LastName,
                BirthDate = input.BirthDate,
                AdmissionDate = input.AdmissionDate,
                Address1 = input.Address1,
                LocalityId = input.LocalityId,
                CountryCode = input.CountryCode,
                Email = input.Email,
                Status = ParseStatus(input.Status) ?? ApplicationStatus.Received,
                DecisionDate = input.DecisionDate,
                DecisionNotes = NormalizeNotes(input.DecisionNotes),
                CreatedAt = DateTime.UtcNow
            };

            db.Applicants.Add(a);
            await db.SaveChangesAsync();   // need the identity value before we can audit it

            AuditLog.Record(db, user, nameof(Applicant), a.Id, "Created",
                $"Applicant {a.FullName} ({a.AlienNumber}) added to the register.");
            await db.SaveChangesAsync();

            // Load the locality so the returned DTO carries city/state/zip.
            await db.Entry(a).Reference(x => x.Locality).LoadAsync();
            return TypedResults.Created($"/api/applicants/{a.Id}", ApplicantDto.From(a));
        })
        .RequireAuthorization(Policies.ManageApplicants)
        .WithName("CreateApplicant");

        g.MapPut("/{id:int}", async Task<Results<Ok<ApplicantDto>, NotFound, ValidationProblem, Conflict<string>>> (
            NaturalizationDbContext db, ClaimsPrincipal user, int id, ApplicantInput input) =>
        {
            if (Validate(input) is { Count: > 0 } errors) return TypedResults.ValidationProblem(errors);

            if (await UnknownLocality(db, input.LocalityId) is { } localityError)
                return TypedResults.ValidationProblem(localityError);

            var a = await db.Applicants.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return TypedResults.NotFound();

            // An edit can collide with somebody else's A-Number just as a create can.
            // The old code didn't check at all and let the DB index throw a 500.
            if (a.AlienNumber != input.AlienNumber)
            {
                var taken = await db.Applicants.IgnoreQueryFilters()
                    .AnyAsync(x => x.AlienNumber == input.AlienNumber && x.Id != id);
                if (taken)
                {
                    return TypedResults.Conflict(
                        $"A-Number {input.AlienNumber} is already on file against another applicant.");
                }
            }

            var changes = Diff(a, input);

            a.AlienNumber = input.AlienNumber;
            a.NaturalizationNumber = (input.NaturalizationNumber ?? "").Trim();
            a.PetitionNumber = (input.PetitionNumber ?? "").Trim();
            a.FirstName = input.FirstName;
            a.MiddleName = NormalizeMiddle(input.MiddleName);
            a.LastName = input.LastName;
            a.BirthDate = input.BirthDate;
            a.AdmissionDate = input.AdmissionDate;
            a.Address1 = input.Address1;
            a.LocalityId = input.LocalityId;
            a.CountryCode = input.CountryCode;
            a.Email = input.Email;
            a.Status = ParseStatus(input.Status) ?? a.Status;
            a.DecisionDate = input.DecisionDate;
            a.DecisionNotes = NormalizeNotes(input.DecisionNotes);
            a.UpdatedAt = DateTime.UtcNow;

            if (changes.Count > 0)
            {
                AuditLog.Record(db, user, nameof(Applicant), a.Id, "Updated",
                    $"Changed {string.Join(", ", changes)}.");
            }

            await db.SaveChangesAsync();

            // Reload the (possibly changed) locality for the returned DTO.
            await db.Entry(a).Reference(x => x.Locality).LoadAsync();
            return TypedResults.Ok(ApplicantDto.From(a));
        })
        .RequireAuthorization(Policies.ManageApplicants)
        .WithName("UpdateApplicant");

        g.MapDelete("/{id:int}", async Task<Results<NoContent, NotFound>> (
            NaturalizationDbContext db, ClaimsPrincipal user, int id) =>
        {
            var a = await db.Applicants.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return TypedResults.NotFound();

            /*
             * Withdraw, do not destroy.
             *
             * This used to be db.Applicants.Remove(a), which cascaded through the
             * applicant's cases, evidence and — fatally — their audit trail. An
             * audit trail you can delete is not an audit trail. The row stays in
             * the file; a query filter hides it.
             */
            a.IsDeleted = true;
            a.DeletedAt = DateTime.UtcNow;
            a.DeletedBy = user.OfficerName();

            AuditLog.Record(db, user, nameof(Applicant), a.Id, "Deleted",
                $"Applicant {a.FullName} ({a.AlienNumber}) withdrawn from the active register. "
                    + "Record and audit trail retained.");

            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        })
        .RequireAuthorization(Policies.WithdrawApplicants)
        .WithName("DeleteApplicant")
        .WithSummary("Withdraw an applicant from the register. Soft delete: nothing is destroyed.");

        g.MapPost("/{id:int}/restore", async Task<Results<Ok<ApplicantDto>, NotFound>> (
            NaturalizationDbContext db, ClaimsPrincipal user, int id) =>
        {
            var a = await db.Applicants.IgnoreQueryFilters()
                .Include(x => x.Locality)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return TypedResults.NotFound();

            if (a.IsDeleted)
            {
                a.IsDeleted = false;
                a.DeletedAt = null;
                a.DeletedBy = null;

                AuditLog.Record(db, user, nameof(Applicant), a.Id, "Restored",
                    $"Applicant {a.FullName} ({a.AlienNumber}) returned to the active register.");

                await db.SaveChangesAsync();
            }

            return TypedResults.Ok(ApplicantDto.From(a));
        })
        .RequireAuthorization(Policies.WithdrawApplicants)
        .WithName("RestoreApplicant")
        .WithSummary("Return a withdrawn applicant to the active register.");
    }

    /// <summary>Trim a middle name and fold blank/whitespace to null, so the
    /// column stays null rather than storing an empty string.</summary>
    private static string? NormalizeMiddle(string? middle) =>
        string.IsNullOrWhiteSpace(middle) ? null : middle.Trim();

    /// <summary>Same fold-blank-to-null treatment for the free-text decision notes.</summary>
    private static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    /// <summary>
    /// A field error if <paramref name="localityId"/> is given but no such
    /// locality exists; null if it is null (residence is optional) or valid. Keeps
    /// a bad FK from surfacing as a raw 500 from the database constraint.
    /// </summary>
    private static async Task<Dictionary<string, string[]>?> UnknownLocality(
        NaturalizationDbContext db, int? localityId)
    {
        if (localityId is not int id) return null;
        var exists = await db.Localities.AnyAsync(l => l.Id == id);
        return exists ? null : new() { ["localityId"] = ["Unknown locality."] };
    }

    /// <summary>Parse the wire status into the enum, tolerantly; null if blank or unknown.</summary>
    private static ApplicationStatus? ParseStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<ApplicationStatus>(status.Trim(), ignoreCase: true, out var parsed)
                ? parsed
                : null;

    private static Dictionary<string, string[]> Validate(ApplicantInput i)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(i.FirstName))
            errors["firstName"] = ["First name is required."];

        if (string.IsNullOrWhiteSpace(i.LastName))
            errors["lastName"] = ["Last name is required."];

        if (string.IsNullOrWhiteSpace(i.AlienNumber))
            errors["alienNumber"] = ["A-Number is required."];

        if (i.BirthDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            errors["birthDate"] = ["Date of birth must be in the past."];

        if (i.AdmissionDate < i.BirthDate)
            errors["admissionDate"] = ["Admission date cannot precede date of birth."];

        if (!string.IsNullOrWhiteSpace(i.Status) && ParseStatus(i.Status) is null)
            errors["status"] = ["Unknown status."];

        return errors;
    }

    /// <summary>
    /// Which fields an edit actually changed. The audit summary names them, so
    /// "Updated" means something more useful than "somebody saved this form".
    /// </summary>
    private static List<string> Diff(Applicant a, ApplicantInput i)
    {
        var changed = new List<string>();
        void Check(string field, object? before, object? after)
        {
            if (!Equals(before, after)) changed.Add(field);
        }

        Check("A-Number", a.AlienNumber, i.AlienNumber);
        Check("naturalization number", a.NaturalizationNumber, (i.NaturalizationNumber ?? "").Trim());
        Check("petition number", a.PetitionNumber, (i.PetitionNumber ?? "").Trim());
        Check("first name", a.FirstName, i.FirstName);
        // Compare the normalised middle so a blank form field ("") vs a null
        // column doesn't log a phantom change on every save.
        Check("middle name", a.MiddleName, NormalizeMiddle(i.MiddleName));
        Check("last name", a.LastName, i.LastName);
        Check("date of birth", a.BirthDate, i.BirthDate);
        Check("admission date", a.AdmissionDate, i.AdmissionDate);
        Check("address", a.Address1, i.Address1);
        Check("residence", a.LocalityId, i.LocalityId);
        Check("country", a.CountryCode, i.CountryCode);
        Check("email", a.Email, i.Email);
        Check("status", a.Status, ParseStatus(i.Status) ?? a.Status);
        Check("decision date", a.DecisionDate, i.DecisionDate);
        Check("decision notes", a.DecisionNotes, NormalizeNotes(i.DecisionNotes));

        return changed;
    }
}
