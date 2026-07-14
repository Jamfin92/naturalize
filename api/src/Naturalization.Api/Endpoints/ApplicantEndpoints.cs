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
                query = query.Where(a =>
                    EF.Functions.Like(a.FullName, $"%{term}%") ||
                    EF.Functions.Like(a.AlienNumber, $"%{term}%"));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(a => a.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => ApplicantDto.From(a))
                .ToListAsync();

            return TypedResults.Ok(new Paged<ApplicantDto>(items, page, pageSize, total));
        })
        .WithName("ListApplicants")
        .WithSummary("List applicants, optionally filtered by name or A-Number.");

        g.MapGet("/{id:int}", async Task<Results<Ok<ApplicantDto>, NotFound>> (NaturalizationDbContext db, int id) =>
        {
            var a = await db.Applicants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return a is null ? TypedResults.NotFound() : TypedResults.Ok(ApplicantDto.From(a));
        })
        .WithName("GetApplicant");

        g.MapGet("/{id:int}/cases", async Task<Results<Ok<List<CaseDto>>, NotFound>> (NaturalizationDbContext db, int id) =>
        {
            if (!await db.Applicants.AnyAsync(a => a.Id == id)) return TypedResults.NotFound();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var cases = await db.Cases.AsNoTracking()
                .Include(c => c.Applicant)
                .Where(c => c.ApplicantId == id)
                .OrderByDescending(c => c.FiledOn)
                .ToListAsync();

            return TypedResults.Ok(cases.Select(c => CaseDto.From(c, today)).ToList());
        })
        .WithName("GetApplicantCases");

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
                FullName = input.FullName,
                DateOfBirth = input.DateOfBirth,
                CountryOfBirth = input.CountryOfBirth,
                Nationality = input.Nationality,
                AddressLine = input.AddressLine,
                City = input.City,
                State = input.State,
                PostalCode = input.PostalCode,
                Email = input.Email,
                Phone = input.Phone,
                LawfulPermanentResidentSince = input.LawfulPermanentResidentSince,
                CreatedAt = DateTime.UtcNow
            };

            db.Applicants.Add(a);
            await db.SaveChangesAsync();   // need the identity value before we can audit it

            AuditLog.Record(db, user, nameof(Applicant), a.Id, "Created",
                $"Applicant {a.FullName} ({a.AlienNumber}) added to the register.");
            await db.SaveChangesAsync();

            return TypedResults.Created($"/api/applicants/{a.Id}", ApplicantDto.From(a));
        })
        .WithName("CreateApplicant");

        g.MapPut("/{id:int}", async Task<Results<Ok<ApplicantDto>, NotFound, ValidationProblem, Conflict<string>>> (
            NaturalizationDbContext db, ClaimsPrincipal user, int id, ApplicantInput input) =>
        {
            if (Validate(input) is { Count: > 0 } errors) return TypedResults.ValidationProblem(errors);

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
            a.FullName = input.FullName;
            a.DateOfBirth = input.DateOfBirth;
            a.CountryOfBirth = input.CountryOfBirth;
            a.Nationality = input.Nationality;
            a.AddressLine = input.AddressLine;
            a.City = input.City;
            a.State = input.State;
            a.PostalCode = input.PostalCode;
            a.Email = input.Email;
            a.Phone = input.Phone;
            a.LawfulPermanentResidentSince = input.LawfulPermanentResidentSince;

            if (changes.Count > 0)
            {
                AuditLog.Record(db, user, nameof(Applicant), a.Id, "Updated",
                    $"Changed {string.Join(", ", changes)}.");
            }

            await db.SaveChangesAsync();
            return TypedResults.Ok(ApplicantDto.From(a));
        })
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
             * applicant's cases, evidence, decisions and — fatally — their entire
             * CaseEvent audit trail. An audit trail you can delete is not an audit
             * trail. The row stays in the file; a query filter hides it.
             *
             * The applicant's CASES are deliberately left unstamped: their own
             * query filter chains through Applicant.IsDeleted, so they disappear
             * anyway, and leaving IsDeleted false on them keeps "hidden because the
             * applicant was withdrawn" distinct from "this case was deleted on its
             * own merits" — which is what makes Restore safe.
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
        .WithName("DeleteApplicant")
        .WithSummary("Withdraw an applicant from the register. Soft delete: nothing is destroyed.");

        g.MapPost("/{id:int}/restore", async Task<Results<Ok<ApplicantDto>, NotFound>> (
            NaturalizationDbContext db, ClaimsPrincipal user, int id) =>
        {
            var a = await db.Applicants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
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
        .WithName("RestoreApplicant")
        .WithSummary("Return a withdrawn applicant to the active register.");
    }

    private static Dictionary<string, string[]> Validate(ApplicantInput i)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(i.FullName))
            errors["fullName"] = ["Full name is required."];

        if (string.IsNullOrWhiteSpace(i.AlienNumber))
            errors["alienNumber"] = ["A-Number is required."];

        if (i.DateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
            errors["dateOfBirth"] = ["Date of birth must be in the past."];

        if (i.LawfulPermanentResidentSince < i.DateOfBirth)
            errors["lawfulPermanentResidentSince"] = ["LPR date cannot precede date of birth."];

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
        Check("name", a.FullName, i.FullName);
        Check("date of birth", a.DateOfBirth, i.DateOfBirth);
        Check("country of birth", a.CountryOfBirth, i.CountryOfBirth);
        Check("nationality", a.Nationality, i.Nationality);
        Check("address", a.AddressLine, i.AddressLine);
        Check("city", a.City, i.City);
        Check("state", a.State, i.State);
        Check("postal code", a.PostalCode, i.PostalCode);
        Check("email", a.Email, i.Email);
        Check("phone", a.Phone, i.Phone);
        Check("LPR date", a.LawfulPermanentResidentSince, i.LawfulPermanentResidentSince);

        return changed;
    }
}
