using Naturalization.Api.Domain;

namespace Naturalization.Api.Services;

/// <summary>
/// Small helpers over <see cref="ApplicationStatus"/>.
///
/// The per-case state machine that used to live here went away with the case
/// table; a single applicant row now carries its own status, set directly. What
/// remains is the one distinction the pipeline metrics still need — whether a
/// status counts as open work — plus a human label for the terse enum names.
/// </summary>
public static class ApplicationStatuses
{
    /// <summary>Statuses that count as still-open work for pipeline metrics.</summary>
    public static bool IsPending(ApplicationStatus status) =>
        status is ApplicationStatus.Received or ApplicationStatus.InReview;

    /// <summary>Terminal states carry no further action.</summary>
    public static bool IsTerminal(ApplicationStatus status) =>
        status is ApplicationStatus.Naturalized or ApplicationStatus.Denied or ApplicationStatus.Withdrawn;

    /// <summary>Human label for a status. Mirrors STATUS_LABELS in the frontend's types.ts.</summary>
    public static string Label(ApplicationStatus status) => status switch
    {
        ApplicationStatus.InReview => "In review",
        _ => status.ToString()
    };
}
