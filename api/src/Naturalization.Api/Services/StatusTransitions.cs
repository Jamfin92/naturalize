using Naturalization.Api.Domain;

namespace Naturalization.Api.Services;

/// <summary>
/// The N-400 lifecycle as an explicit state machine.
///
/// This is the only place transitions are defined. The API exposes the legal
/// next states on every case read, and the UI renders exactly those as buttons
/// rather than duplicating the rules in TypeScript — so the interface cannot
/// offer a move the server would reject, and the two cannot drift apart.
/// </summary>
public static class StatusTransitions
{
    private static readonly Dictionary<CaseStatus, CaseStatus[]> Allowed = new()
    {
        [CaseStatus.Received] = [CaseStatus.BiometricsScheduled, CaseStatus.Withdrawn],
        [CaseStatus.BiometricsScheduled] = [CaseStatus.BiometricsCompleted, CaseStatus.Withdrawn],
        [CaseStatus.BiometricsCompleted] = [CaseStatus.InterviewScheduled, CaseStatus.Withdrawn],
        [CaseStatus.InterviewScheduled] = [CaseStatus.InterviewCompleted, CaseStatus.Withdrawn],

        // The interview is the gate: a case can only be adjudicated once it is held.
        [CaseStatus.InterviewCompleted] = [CaseStatus.Approved, CaseStatus.Denied, CaseStatus.Withdrawn],

        [CaseStatus.Approved] = [CaseStatus.OathScheduled],
        [CaseStatus.OathScheduled] = [CaseStatus.Naturalized],

        // Terminal. Naturalization, denial and withdrawal all end the case.
        [CaseStatus.Naturalized] = [],
        [CaseStatus.Denied] = [],
        [CaseStatus.Withdrawn] = []
    };

    public static IReadOnlyList<CaseStatus> From(CaseStatus status) =>
        Allowed.TryGetValue(status, out var next) ? next : [];

    public static bool IsAllowed(CaseStatus from, CaseStatus to) => From(from).Contains(to);

    public static bool IsTerminal(CaseStatus status) => From(status).Count == 0;

    /// <summary>Statuses that count as still-open work for pipeline metrics.</summary>
    public static bool IsPending(CaseStatus status) =>
        status is not (CaseStatus.Naturalized or CaseStatus.Denied or CaseStatus.Withdrawn);
}
