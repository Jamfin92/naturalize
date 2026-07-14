using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;
using Naturalization.Api.Dtos;

namespace Naturalization.Api.Services;

/// <summary>
/// Caseload statistics. Shared by the dashboard endpoint and the pipeline PDF,
/// so the two can never disagree about how many cases are pending.
/// </summary>
public class CaseMetrics(NaturalizationDbContext db)
{
    public async Task<MetricsDto> ComputeAsync()
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var totalApplicants = await db.Applicants.CountAsync();

        var statusCounts = await db.Cases.AsNoTracking()
            .GroupBy(c => c.Status)
            .Select(gr => new { Status = gr.Key, Count = gr.Count() })
            .ToListAsync();

        var pending = statusCounts
            .Where(x => StatusTransitions.IsPending(x.Status))
            .Sum(x => x.Count);

        var approvedThisMonth = await db.Decisions.AsNoTracking()
            .CountAsync(d => d.Outcome == DecisionOutcome.Approved && d.DecidedOn >= monthStart);

        var deniedThisMonth = await db.Decisions.AsNoTracking()
            .CountAsync(d => d.Outcome == DecisionOutcome.Denied && d.DecidedOn >= monthStart);

        var recentEvents = await db.Events.AsNoTracking()
            .OrderByDescending(e => e.OccurredAt)
            .Take(8)
            .Select(e => CaseEventDto.From(e))
            .ToListAsync();

        // Every status appears, including those with no cases — a pipeline chart
        // with rows silently missing is worse than one showing an honest zero.
        var ordered = Enum.GetValues<CaseStatus>()
            .Select(s => new StatusCountDto(
                s.ToString(),
                statusCounts.FirstOrDefault(x => x.Status == s)?.Count ?? 0))
            .ToList();

        return new MetricsDto(
            totalApplicants,
            pending,
            approvedThisMonth,
            deniedThisMonth,
            await MedianDaysToDecisionAsync(),
            ordered,
            recentEvents);
    }

    /// <summary>
    /// Median, not mean: a handful of cases stuck in limbo for three years
    /// would drag an average far away from what a typical applicant experiences.
    /// </summary>
    public async Task<int> MedianDaysToDecisionAsync()
    {
        var spans = await db.Decisions.AsNoTracking()
            .Include(d => d.Case)
            .Select(d => d.DecidedOn.DayNumber - d.Case.FiledOn.DayNumber)
            .ToListAsync();

        if (spans.Count == 0) return 0;

        spans.Sort();
        var mid = spans.Count / 2;

        return spans.Count % 2 == 0
            ? (spans[mid - 1] + spans[mid]) / 2
            : spans[mid];
    }

    /// <summary>Open cases, oldest first — the aging report's backbone.</summary>
    public async Task<List<NaturalizationCase>> OldestPendingAsync(int take)
    {
        var all = await db.Cases.AsNoTracking()
            .Include(c => c.Applicant)
            .ToListAsync();

        return all
            .Where(c => StatusTransitions.IsPending(c.Status))
            .OrderBy(c => c.FiledOn)
            .Take(take)
            .ToList();
    }
}
