namespace Naturalization.Api.Domain;

public class Applicant
{
    public int Id { get; set; }

    /// <summary>USCIS alien registration number, e.g. "A123456789".</summary>
    public string AlienNumber { get; set; } = "";

    public string FullName { get; set; } = "";
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

    public List<NaturalizationCase> Cases { get; set; } = [];
}

public class NaturalizationCase
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
/// Append-only audit trail. Never updated, never deleted while its case lives —
/// this is the record of who did what to someone's citizenship application.
/// </summary>
public class CaseEvent
{
    public int Id { get; set; }

    public int CaseId { get; set; }
    public NaturalizationCase Case { get; set; } = null!;

    public string EventType { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string Actor { get; set; } = "";
    public string Notes { get; set; } = "";
}
