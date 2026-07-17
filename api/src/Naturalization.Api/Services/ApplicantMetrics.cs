using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;

namespace Naturalization.Api.Services;

/// <summary>One status and how many applicants are in it.</summary>
public record StatusCount(ApplicationStatus Status, int Count);

/// <summary>Headline caseload numbers, computed once and shared by the reports.</summary>
public record PipelineSummary(
    int TotalApplicants,
    int Pending,
    int ApprovedThisMonth,
    int DeniedThisMonth,
    IReadOnlyList<StatusCount> StatusCounts);

/// <summary>
/// Caseload statistics over the applicant register. Now that a case is just a row
/// on the applicant, these read straight off <see cref="Applicant.Status"/> and
/// <see cref="Applicant.DecisionDate"/> rather than a separate case table.
/// </summary>
public class ApplicantMetrics(NaturalizationDbContext db)
{
    public async Task<PipelineSummary> ComputeAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var total = await db.Applicants.CountAsync();

        var counts = await db.Applicants.AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(gr => new { Status = gr.Key, Count = gr.Count() })
            .ToListAsync();

        var pending = counts
            .Where(x => ApplicationStatuses.IsPending(x.Status))
            .Sum(x => x.Count);

        var approvedThisMonth = await db.Applicants.AsNoTracking()
            .CountAsync(a => (a.Status == ApplicationStatus.Approved || a.Status == ApplicationStatus.Naturalized)
                && a.DecisionDate != null && a.DecisionDate >= monthStart);

        var deniedThisMonth = await db.Applicants.AsNoTracking()
            .CountAsync(a => a.Status == ApplicationStatus.Denied
                && a.DecisionDate != null && a.DecisionDate >= monthStart);

        // Every status appears, including those with no applicants — a pipeline
        // chart with rows silently missing is worse than one showing an honest zero.
        var ordered = Enum.GetValues<ApplicationStatus>()
            .Select(s => new StatusCount(s, counts.FirstOrDefault(x => x.Status == s)?.Count ?? 0))
            .ToList();

        return new PipelineSummary(total, pending, approvedThisMonth, deniedThisMonth, ordered);
    }

    /// <summary>
    /// Open applicants, oldest record first — the aging report's backbone. With
    /// no per-case filing date any more, "oldest" is by when the record was
    /// created.
    /// </summary>
    public async Task<List<Applicant>> OldestPendingAsync(int take)
    {
        return await db.Applicants.AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Received || a.Status == ApplicationStatus.InReview)
            .OrderBy(a => a.CreatedAt)
            .Take(take)
            .ToListAsync();
    }
}
