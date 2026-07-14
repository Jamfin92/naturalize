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
