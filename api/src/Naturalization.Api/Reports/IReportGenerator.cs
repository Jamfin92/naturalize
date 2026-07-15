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
    /// <summary>The complete file for one case: particulars, timeline, evidence, decision.</summary>
    Task<byte[]> CaseRecordAsync(int caseId, CancellationToken ct = default);

    /// <summary>Decisions in a date range, with approve/deny counts by field office.</summary>
    Task<byte[]> ApprovalsAsync(DateOnly from, DateOnly to, string? fieldOffice, CancellationToken ct = default);

    /// <summary>Current caseload by status, with aging and the oldest pending matters.</summary>
    Task<byte[]> PipelineAsync(CancellationToken ct = default);

    /// <summary>One mailing label per active applicant, laid out for Avery 5160 label sheets.</summary>
    Task<byte[]> MailingLabelsAsync(CancellationToken ct = default);
}
