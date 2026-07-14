using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;
using Naturalization.Api.Dtos;

namespace Naturalization.Api.Endpoints;

public static class ApplicantEndpoints
{
    public static void MapApplicants(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/applicants").WithTags("Applicants");

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

        g.MapPost("/", async Task<Results<Created<ApplicantDto>, ValidationProblem>> (
            NaturalizationDbContext db, ApplicantInput input) =>
        {
            if (Validate(input) is { Count: > 0 } errors) return TypedResults.ValidationProblem(errors);

            if (await db.Applicants.AnyAsync(a => a.AlienNumber == input.AlienNumber))
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
            await db.SaveChangesAsync();

            return TypedResults.Created($"/api/applicants/{a.Id}", ApplicantDto.From(a));
        })
        .WithName("CreateApplicant");

        g.MapPut("/{id:int}", async Task<Results<Ok<ApplicantDto>, NotFound, ValidationProblem>> (
            NaturalizationDbContext db, int id, ApplicantInput input) =>
        {
            if (Validate(input) is { Count: > 0 } errors) return TypedResults.ValidationProblem(errors);

            var a = await db.Applicants.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return TypedResults.NotFound();

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

            await db.SaveChangesAsync();
            return TypedResults.Ok(ApplicantDto.From(a));
        })
        .WithName("UpdateApplicant");

        g.MapDelete("/{id:int}", async Task<Results<NoContent, NotFound>> (NaturalizationDbContext db, int id) =>
        {
            var a = await db.Applicants.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return TypedResults.NotFound();

            // Cascades to cases, documents, events and the decision — see the
            // DbContext. Deleting an applicant erases their audit trail too,
            // which is why a real deployment should soft-delete instead.
            db.Applicants.Remove(a);
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        })
        .WithName("DeleteApplicant");
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
}