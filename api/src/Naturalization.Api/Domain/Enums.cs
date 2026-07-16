namespace Naturalization.Api.Domain;

/// <summary>
/// The lifecycle of an N-400. Persisted as strings, not ints — an integer
/// column would silently re-map every stored row the moment someone inserts
/// a new member into the middle of this enum.
/// </summary>
public enum CaseStatus
{
    Received,
    BiometricsScheduled,
    BiometricsCompleted,
    InterviewScheduled,
    InterviewCompleted,
    Approved,
    Denied,
    OathScheduled,
    Naturalized,
    Withdrawn
}

public enum DecisionOutcome
{
    Approved,
    Denied,

    /// <summary>Adjudication deferred — typically pending further evidence.</summary>
    Continued
}

public enum DocumentStatus
{
    Received,
    Verified,
    Rejected
}

/// <summary>
/// What an officer is allowed to do. Persisted as a string, like the other
/// enums, so the stored value stays legible and does not silently re-map if a
/// member is ever inserted into the middle of this list.
///
/// The levels are cumulative in intent: a Viewer can read, an Officer can also
/// create and update applicants, and an Admin can additionally withdraw and
/// restore records. The endpoints enforce this via authorization policies; see
/// Auth/Policies.cs.
/// </summary>
public enum OfficerRole
{
    /// <summary>Read-only. Can view the register, cases, reports and history.</summary>
    Viewer,

    /// <summary>Can create and update applicant records, on top of everything a Viewer can do.</summary>
    Officer,

    /// <summary>Full access, including withdrawing and restoring records.</summary>
    Admin
}
