using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;
using Naturalization.Api.Dtos;
using Naturalization.Api.Services;

namespace Naturalization.Api.Endpoints;

public static class DecisionEndpoints
{
    public static void MapDecisions(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/decisions").WithTags("Decisions");

        g.MapGet("/", async (NaturalizationDbContext db, DateOnly? from, DateOnly? to, string? fieldOffice) =>
        {
            var query = db.Decisions.AsNoTracking()
                .Include(d => d.Case).ThenInclude(c => c.Applicant)
                .AsQueryable();

            if (from is not null) query = query.Where(d => d.DecidedOn >= from);
            if (to is not null) query = query.Where(d => d.DecidedOn <= to);
            if (!string.IsNullOrWhiteSpace(fieldOffice))
                query = query.Where(d => d.Case.FieldOffice == fieldOffice);

            var items = await query
                .OrderByDescending(d => d.DecidedOn)
                .Select(d => DecisionDto.From(d))
                .ToListAsync();

            return TypedResults.Ok(items);
        })
        .WithName("ListDecisions")
        .WithSummary("List adjudication decisions, optionally by date range and field office.");

        g.MapGet("/{id:int}", async Task<Results<Ok<DecisionDto>, NotFound>> (NaturalizationDbContext db, int id) =>
        {
            var d = await db.Decisions.AsNoTracking()
                .Include(x => x.Case).ThenInclude(c => c.Applicant)
                .FirstOrDefaultAsync(x => x.Id == id);

            return d is null ? TypedResults.NotFound() : TypedResults.Ok(DecisionDto.From(d));
        })
        .WithName("GetDecision");

        /*
         * Recording a decision does three things atomically: writes the Decision
         * row, advances the case status to match, and appends an audit event.
         * They must not be able to drift apart, so they share one SaveChanges.
         */
        g.MapPost("/", async Task<Results<Created<DecisionDto>, ValidationProblem>> (
            NaturalizationDbContext db, DecisionInput input) =>
        {
            var errors = new Dictionary<string, string[]>();

            var c = await db.Cases
                .Include(x => x.Applicant)
                .Include(x => x.Decision)
                .FirstOrDefaultAsync(x => x.Id == input.CaseId);

            if (c is null)
            {
                errors["caseId"] = [$"No case with id {input.CaseId}."];
                return TypedResults.ValidationProblem(errors);
            }

            if (c.Decision is not null)
            {
                var detail = $"Case {c.ReceiptNumber} has already been decided ({c.Decision.Outcome}). " +
                             "A decision is final; reopening requires a new case.";
                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]> { ["caseId"] = [detail] }, detail: detail);
            }

            if (!Enum.TryParse<DecisionOutcome>(input.Outcome, out var outcome))
            {
                errors["outcome"] = [$"'{input.Outcome}' is not a valid outcome."];
                return TypedResults.ValidationProblem(errors);
            }

            // A case can only be adjudicated once the interview has actually happened.
            if (c.Status != CaseStatus.InterviewCompleted)
            {
                var detail = $"Case {c.ReceiptNumber} is '{c.Status}'. A decision can only be recorded " +
                             "once the interview is completed.";
                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]> { ["caseId"] = [detail] }, detail: detail);
            }

            if (outcome == DecisionOutcome.Denied && string.IsNullOrWhiteSpace(input.DenialReasonCode))
                errors["denialReasonCode"] = ["A denial must cite a statutory reason code."];

            if (string.IsNullOrWhiteSpace(input.Rationale))
                errors["rationale"] = ["A rationale is required for every decision."];

            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);

            var decision = new Decision
            {
                CaseId = c.Id,
                Outcome = outcome,
                DecidedOn = today,
                DecidedBy = string.IsNullOrWhiteSpace(input.DecidedBy) ? "Unknown officer" : input.DecidedBy,
                Rationale = input.Rationale,
                DenialReasonCode = outcome == DecisionOutcome.Denied ? input.DenialReasonCode : null
            };
            db.Decisions.Add(decision);

            /*
             * Approved/Denied move the case on. "Continued" deliberately does
             * not: the case stays at InterviewCompleted awaiting more evidence.
             * But a Decision row is unique per case, so a continued case can
             * never be decided again — which is a real modelling wart. See the
             * note in README ("Known limitations").
             */
            if (outcome is DecisionOutcome.Approved or DecisionOutcome.Denied)
            {
                var target = outcome == DecisionOutcome.Approved ? CaseStatus.Approved : CaseStatus.Denied;

                if (!StatusTransitions.IsAllowed(c.Status, target))
                {
                    var detail = $"Cannot move from '{c.Status}' to '{target}'.";
                    return TypedResults.ValidationProblem(
                        new Dictionary<string, string[]> { ["outcome"] = [detail] }, detail: detail);
                }

                c.Status = target;
            }

            c.Events.Add(new CaseEvent
            {
                EventType = $"Decision recorded: {outcome}",
                OccurredAt = now,
                Actor = decision.DecidedBy,
                Notes = decision.DenialReasonCode is null
                    ? decision.Rationale
                    : $"[{decision.DenialReasonCode}] {decision.Rationale}"
            });

            await db.SaveChangesAsync();

            decision.Case = c;
            return TypedResults.Created($"/api/decisions/{decision.Id}", DecisionDto.From(decision));
        })
        .WithName("CreateDecision")
        .WithSummary("Record an adjudication decision and advance the case accordingly.");

        g.MapDelete("/{id:int}", async Task<Results<NoContent, NotFound>> (NaturalizationDbContext db, int id) =>
        {
            var d = await db.Decisions.FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return TypedResults.NotFound();

            db.Decisions.Remove(d);
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        })
        .WithName("DeleteDecision")
        .WithSummary("Rescind a decision. Present for CRUD completeness; a real deployment should not expose this.");
    }
}