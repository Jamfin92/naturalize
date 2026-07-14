using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;
using Naturalization.Api.Dtos;
using Naturalization.Api.Services;

namespace Naturalization.Api.Endpoints;

public static class CaseEndpoints
{
    public static void MapCases(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases").WithTags("Cases");

        g.MapGet("/", async (NaturalizationDbContext db, string? q, string? status, int page = 1, int pageSize = 15) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var query = db.Cases.AsNoTracking().Include(c => c.Applicant).AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CaseStatus>(status, out var s))
                query = query.Where(c => c.Status == s);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(c =>
                    EF.Functions.Like(c.ReceiptNumber, $"%{term}%") ||
                    EF.Functions.Like(c.Applicant.FullName, $"%{term}%"));
            }

            var total = await query.CountAsync();
            var cases = await query
                .OrderBy(c => c.FiledOn) // oldest first: the ones aging out matter most
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = cases.Select(c => CaseDto.From(c, today)).ToList();
            return TypedResults.Ok(new Paged<CaseDto>(items, page, pageSize, total));
        })
        .WithName("ListCases")
        .WithSummary("List cases, filtered by status and/or receipt number or applicant name.");

        g.MapGet("/{id:int}", async Task<Results<Ok<CaseDetailDto>, NotFound>> (NaturalizationDbContext db, int id) =>
        {
            var c = await db.Cases.AsNoTracking()
                .Include(x => x.Applicant)
                .Include(x => x.Documents)
                .Include(x => x.Events)
                .Include(x => x.Decision)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (c is null) return TypedResults.NotFound();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            return TypedResults.Ok(new CaseDetailDto(
                c.Id, c.ApplicantId, c.Applicant.FullName, c.Applicant.AlienNumber,
                c.ReceiptNumber, c.FiledOn, c.FieldOffice, c.Status.ToString(),
                c.BiometricsOn, c.InterviewOn, c.OathOn,
                today.DayNumber - c.FiledOn.DayNumber,
                ApplicantDto.From(c.Applicant),
                c.Documents.OrderBy(d => d.UploadedAt).Select(DocumentDto.From),
                c.Events.OrderByDescending(e => e.OccurredAt).Select(CaseEventDto.From),
                c.Decision is null ? null : DecisionDto.From(c.Decision),
                // The UI renders exactly these as buttons; see StatusTransitions.
                StatusTransitions.From(c.Status).Select(s => s.ToString())));
        })
        .WithName("GetCase");

        g.MapGet("/{id:int}/events", async Task<Results<Ok<List<CaseEventDto>>, NotFound>> (
            NaturalizationDbContext db, int id) =>
        {
            if (!await db.Cases.AnyAsync(c => c.Id == id)) return TypedResults.NotFound();

            var events = await db.Events.AsNoTracking()
                .Where(e => e.CaseId == id)
                .OrderByDescending(e => e.OccurredAt)
                .Select(e => CaseEventDto.From(e))
                .ToListAsync();

            return TypedResults.Ok(events);
        })
        .WithName("GetCaseEvents");

        g.MapPost("/", async Task<Results<Created<CaseDto>, ValidationProblem>> (
            NaturalizationDbContext db, CaseInput input) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (!await db.Applicants.AnyAsync(a => a.Id == input.ApplicantId))
                errors["applicantId"] = [$"No applicant with id {input.ApplicantId}."];

            if (string.IsNullOrWhiteSpace(input.ReceiptNumber))
                errors["receiptNumber"] = ["Receipt number is required."];
            else if (await db.Cases.AnyAsync(c => c.ReceiptNumber == input.ReceiptNumber))
                errors["receiptNumber"] = [$"Receipt number {input.ReceiptNumber} is already on file."];

            if (input.FiledOn > DateOnly.FromDateTime(DateTime.UtcNow))
                errors["filedOn"] = ["Filing date cannot be in the future."];

            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            var c = new NaturalizationCase
            {
                ApplicantId = input.ApplicantId,
                ReceiptNumber = input.ReceiptNumber,
                FiledOn = input.FiledOn,
                FieldOffice = input.FieldOffice,
                Status = CaseStatus.Received
            };
            c.Events.Add(new CaseEvent
            {
                EventType = "Application received",
                OccurredAt = DateTime.UtcNow,
                Actor = "System",
                Notes = $"N-400 recorded at {input.FieldOffice}."
            });

            db.Cases.Add(c);
            await db.SaveChangesAsync();

            await db.Entry(c).Reference(x => x.Applicant).LoadAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return TypedResults.Created($"/api/cases/{c.Id}", CaseDto.From(c, today));
        })
        .WithName("CreateCase");

        /*
         * Status changes go through here rather than through a general PUT, so
         * that every transition is validated against the state machine AND
         * writes an audit event. There is deliberately no endpoint that lets a
         * caller set Status to an arbitrary value.
         */
        g.MapPost("/{id:int}/status", async Task<Results<Ok<CaseDto>, NotFound, ValidationProblem>> (
            NaturalizationDbContext db, int id, TransitionInput input) =>
        {
            var c = await db.Cases.Include(x => x.Applicant).FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return TypedResults.NotFound();

            if (!Enum.TryParse<CaseStatus>(input.Status, out var target))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [$"'{input.Status}' is not a valid case status."]
                });
            }

            if (!StatusTransitions.IsAllowed(c.Status, target))
            {
                var allowed = StatusTransitions.From(c.Status);
                var detail = allowed.Count == 0
                    ? $"Case is in terminal status '{c.Status}' and cannot be advanced."
                    : $"Cannot move from '{c.Status}' to '{target}'. Allowed: {string.Join(", ", allowed)}.";

                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]> { ["status"] = [detail] },
                    detail: detail);
            }

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);

            c.Status = target;

            // Stamp the milestone date the transition implies.
            switch (target)
            {
                case CaseStatus.BiometricsScheduled:
                    c.BiometricsOn ??= today.AddDays(21);
                    break;
                case CaseStatus.InterviewScheduled:
                    c.InterviewOn ??= today.AddDays(30);
                    break;
                case CaseStatus.OathScheduled:
                    c.OathOn ??= today.AddDays(45);
                    break;
            }

            c.Events.Add(new CaseEvent
            {
                EventType = $"Status changed to {target}",
                OccurredAt = now,
                Actor = string.IsNullOrWhiteSpace(input.Actor) ? "Unknown officer" : input.Actor,
                Notes = input.Notes ?? ""
            });

            await db.SaveChangesAsync();
            return TypedResults.Ok(CaseDto.From(c, today));
        })
        .WithName("TransitionCase")
        .WithSummary("Advance a case to a new status. Rejects transitions the state machine disallows.");

        g.MapDelete("/{id:int}", async Task<Results<NoContent, NotFound>> (NaturalizationDbContext db, int id) =>
        {
            var c = await db.Cases.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return TypedResults.NotFound();

            db.Cases.Remove(c);
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        })
        .WithName("DeleteCase");
    }
}