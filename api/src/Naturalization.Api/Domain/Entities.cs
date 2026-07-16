using System.ComponentModel.DataAnnotations.Schema;

namespace Naturalization.Api.Domain;

/// <summary>
/// A record that is withdrawn from the active register rather than destroyed.
///
/// Deleting an applicant used to cascade through their cases and take the audit
/// trail with it. An audit trail you can destroy is not an audit trail, so
/// nothing here is ever removed: it is stamped, hidden by a query filter, and
/// left in the file. See NaturalizationDbContext.OnModelCreating.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}

public class Applicant : ISoftDeletable
{
    public int Id { get; set; }

    /// <summary>USCIS alien registration number, e.g. "A123456789".</summary>
    public string AlienNumber { get; set; } = "";

    public string FirstName { get; set; } = "";

    /// <summary>Optional. Absent for the many applicants who have no middle name.</summary>
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = "";

    /// <summary>
    /// Display name, "First Middle Last" with blanks skipped. Computed, not a
    /// column: [NotMapped] keeps EF from treating this get-only property as a
    /// mapped field (which would fail materialization). Every read site — the
    /// reports, the audit summaries, the DTO mappers — reads this, so splitting
    /// the stored name into parts left them untouched. The only reads that must
    /// use the real columns are the SQL-translated ones (search LIKE, OrderBy).
    /// </summary>
    [NotMapped]
    public string FullName =>
        string.Join(" ", new[] { FirstName, MiddleName, LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

    public DateOnly DateOfBirth { get; set; }
    public string CountryOfBirth { get; set; } = "";
    public string Nationality { get; set; } = "";

    public string AddressLine { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";

    /// <summary>
    /// Date the applicant became a lawful permanent resident. The statutory
    /// continuous-residence clock (INA 316(a)) runs from here.
    /// </summary>
    public DateOnly LawfulPermanentResidentSince { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public List<NaturalizationCase> Cases { get; set; } = [];
}

public class NaturalizationCase : ISoftDeletable
{
    public int Id { get; set; }

    public int ApplicantId { get; set; }
    public Applicant Applicant { get; set; } = null!;

    /// <summary>USCIS receipt number, e.g. "NBC2024123456".</summary>
    public string ReceiptNumber { get; set; } = "";

    public DateOnly FiledOn { get; set; }
    public string FieldOffice { get; set; } = "";
    public CaseStatus Status { get; set; } = CaseStatus.Received;

    public DateOnly? BiometricsOn { get; set; }
    public DateOnly? InterviewOn { get; set; }
    public DateOnly? OathOn { get; set; }

    /*
     * Soft-deleting an APPLICANT does not stamp these on their cases. The query
     * filter on NaturalizationCase already chains through Applicant.IsDeleted, so
     * the cases vanish from every read path anyway — and leaving these null keeps
     * the distinction between "hidden because the applicant was withdrawn" and
     * "this case was deleted on its own merits". Restore would otherwise resurrect
     * cases that were meant to stay gone.
     */
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public List<EvidenceDocument> Documents { get; set; } = [];
    public List<CaseEvent> Events { get; set; } = [];
    public Decision? Decision { get; set; }
}

/// <summary>
/// The adjudication record. One per case: a case is decided once, and a
/// reversal is a new case, not an edit to this row.
/// </summary>
public class Decision
{
    public int Id { get; set; }

    public int CaseId { get; set; }
    public NaturalizationCase Case { get; set; } = null!;

    public DecisionOutcome Outcome { get; set; }
    public DateOnly DecidedOn { get; set; }
    public string DecidedBy { get; set; } = "";
    public string Rationale { get; set; } = "";

    /// <summary>Statutory basis for a denial, e.g. "316(a)". Null unless denied.</summary>
    public string? DenialReasonCode { get; set; }
}

public class EvidenceDocument
{
    public int Id { get; set; }

    public int CaseId { get; set; }
    public NaturalizationCase Case { get; set; } = null!;

    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }

    /// <summary>
    /// Content hash. Evidence is stored content-addressed, so re-uploading the
    /// same file does not duplicate it and any later tampering is detectable.
    /// </summary>
    public string Sha256 { get; set; } = "";

    public DocumentStatus Status { get; set; } = DocumentStatus.Received;
    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// Append-only trail of a case's own lifecycle — filed, biometrics, interview,
/// decision, oath. Printed verbatim into the Case Record PDF. Never updated,
/// never deleted: this is the record of who did what to someone's citizenship
/// application.
/// </summary>
public class CaseEvent
{
    public int Id { get; set; }

    public int CaseId { get; set; }
    public NaturalizationCase Case { get; set; } = null!;

    public string EventType { get; set; } = "";
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// The officer, taken from the bearer token — never from the request body.
    /// A trail the caller can sign with someone else's name is not a trail.
    /// </summary>
    public string Actor { get; set; } = "";
    public string Notes { get; set; } = "";
}

/// <summary>
/// System-level audit log: who touched which row, and when.
///
/// Distinct from <see cref="CaseEvent"/> on purpose. CaseEvent is a domain
/// timeline hanging off a case; this is infrastructure. Two reasons it cannot be
/// folded into CaseEvent:
///
///   1. Applicant create/update/delete was previously recorded NOWHERE. The one
///      thing this application does left no trace at all.
///   2. An applicant may have zero cases, so a case-scoped event has nowhere to
///      hang.
///
/// Deliberately has no foreign key, no soft-delete flag and no query filter: it
/// must outlive the row it describes, which is the entire point.
/// </summary>
public class AuditEvent
{
    public int Id { get; set; }

    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }

    /// <summary>Created, Updated, Deleted, Restored.</summary>
    public string Action { get; set; } = "";

    public string Actor { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string Summary { get; set; } = "";
}

/// <summary>
/// A caseworker who can sign in.
///
/// Carries a <see cref="Role"/>: authorisation is no longer a stub. The role is
/// stamped into the bearer token (see Auth/TokenIssuer) and enforced by the
/// applicant endpoints through the policies in Auth/Policies.cs — a Viewer can
/// read but not change records, an Officer can create and update them, and an
/// Admin can additionally withdraw and restore.
/// </summary>
public class OfficerAccount
{
    public int Id { get; set; }

    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string FieldOffice { get; set; } = "";

    /// <summary>
    /// What this officer may do. Defaults to <see cref="OfficerRole.Officer"/> —
    /// the everyday caseworker level — so a hand-inserted account without an
    /// explicit role can still do its job but cannot withdraw records.
    /// </summary>
    public OfficerRole Role { get; set; } = OfficerRole.Officer;

    /// <summary>PBKDF2, via ASP.NET's <c>IPasswordHasher</c>. Never a plaintext password.</summary>
    public string PasswordHash { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
