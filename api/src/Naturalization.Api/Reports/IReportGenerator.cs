namespace Naturalization.Api.Reports;

/// <summary>
/// The seam between "what a report says" and "what draws it".
///
/// The implementation is MigraDoc (MIT). QuestPDF has the nicer API, but its
/// Community licence is free only below a revenue threshold — a field-of-use
/// restriction that is not OSI-approved and that would silently bind every
/// downstream fork of this project. Wrong dependency to hand a legal-aid clinic.
/// Anyone under that threshold who wants QuestPDF in a private fork replaces
/// exactly this one implementation.
/// </summary>
public interface IReportGenerator
{
    /// <summary>Decisions in a date range, with approve/deny counts.</summary>
    Task<byte[]> ApprovalsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Current caseload by status, with aging and the oldest pending matters.</summary>
    Task<byte[]> PipelineAsync(CancellationToken ct = default);

    /// <summary>
    /// One mailing label per active applicant, laid out for Avery 5160 label sheets.
    /// Optionally restricted to applicants added within a date range (on their
    /// <c>CreatedAt</c> day): pass the same day as both bounds for a single date, one
    /// bound for an open-ended range, or neither for every active applicant.
    /// </summary>
    Task<byte[]> MailingLabelsAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);
}
