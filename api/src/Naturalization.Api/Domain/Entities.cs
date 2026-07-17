using System.ComponentModel.DataAnnotations.Schema;

namespace Naturalization.Api.Domain;

/// <summary>
/// A record that is withdrawn from the active register rather than destroyed.
///
/// Deleting an applicant used to take the audit trail with it. An audit trail you
/// can destroy is not an audit trail, so nothing here is ever removed: it is
/// stamped, hidden by a query filter, and left in the file. See
/// NaturalizationDbContext.OnModelCreating.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}

/// <summary>
/// The naturalization record. One row per applicant, and — since the streamlining
/// that folded the case, decision and evidence tables away — the whole of a
/// person's file: their particulars, where the application stands (<see
/// cref="Status"/>) and how it was decided (<see cref="DecisionDate"/> /
/// <see cref="DecisionNotes"/>).
///
/// Town and country are stored as <em>codes</em> (see <see cref="TownCode"/> and
/// <see cref="CountryCode"/>), not free text, so the register speaks one
/// vocabulary and the lookups can be maintained in one place.
/// </summary>
public class Applicant : ISoftDeletable
{
    public int Id { get; set; }

    /// <summary>USCIS alien registration number, e.g. "A123456789".</summary>
    public string AlienNumber { get; set; } = "";

    /// <summary>Certificate of Naturalization number. Assigned on naturalization; blank until then.</summary>
    public string NaturalizationNumber { get; set; } = "";

    /// <summary>USCIS receipt/petition number for the N-400, e.g. "NBC2024123456".</summary>
    public string PetitionNumber { get; set; } = "";

    public string FirstName { get; set; } = "";

    /// <summary>Optional. Absent for the many applicants who have no middle name.</summary>
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = "";

    /// <summary>
    /// Display name, "First Middle Last" with blanks skipped. Computed, not a
    /// column: [NotMapped] keeps EF from treating this get-only property as a
    /// mapped field. Every read site — the reports, the audit summaries, the DTO
    /// mappers — reads this, so it stays even though it is not stored. The only
    /// reads that must use the real columns are the SQL-translated ones (search
    /// LIKE, OrderBy).
    /// </summary>
    [NotMapped]
    public string FullName =>
        string.Join(" ", new[] { FirstName, MiddleName, LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

    public DateOnly BirthDate { get; set; }

    /// <summary>
    /// Date the applicant was admitted as a lawful permanent resident. The
    /// statutory continuous-residence clock (INA 316(a)) runs from here.
    /// </summary>
    public DateOnly AdmissionDate { get; set; }

    public string Address1 { get; set; } = "";

    /// <summary>Town code — the <see cref="Domain.TownCode.Code"/> of a TownCode row.</summary>
    public string TownCode { get; set; } = "";

    /// <summary>Country code — the <see cref="Domain.CountryCode.Code"/> of a CountryCode row.</summary>
    public string CountryCode { get; set; } = "";

    public string ZipCode { get; set; } = "";
    public string Email { get; set; } = "";

    /// <summary>Where the application stands in its lifecycle.</summary>
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Received;

    /// <summary>When the application was adjudicated. Null until it is decided.</summary>
    public DateOnly? DecisionDate { get; set; }

    /// <summary>Free-text basis for the decision. Null until decided.</summary>
    public string? DecisionNotes { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Last edit. Null on a record that has never been changed since creation.</summary>
    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// A town lookup. <see cref="BaseCode"/> is "T" for every row here — it exists so
/// TownCode and <see cref="CountryCode"/> share one shape and could, if ever
/// wanted, be read from one place. <see cref="Code"/> is the short (≤3 char)
/// token stored on <see cref="Applicant.TownCode"/>; <see cref="Description"/> is
/// what a human reads.
/// </summary>
public class TownCode
{
    public int Id { get; set; }

    /// <summary>"T" for a town. (Countries carry "C".)</summary>
    public string BaseCode { get; set; } = "T";

    /// <summary>Up to three characters, mapping to <see cref="Applicant.TownCode"/>.</summary>
    public string Code { get; set; } = "";

    public string Description { get; set; } = "";
}

/// <summary>
/// A country lookup. <see cref="BaseCode"/> is "C" for every row. Same shape as
/// <see cref="TownCode"/>; <see cref="Code"/> maps to <see cref="Applicant.CountryCode"/>.
/// </summary>
public class CountryCode
{
    public int Id { get; set; }

    /// <summary>"C" for a country. (Towns carry "T".)</summary>
    public string BaseCode { get; set; } = "C";

    /// <summary>Up to three characters, mapping to <see cref="Applicant.CountryCode"/>.</summary>
    public string Code { get; set; } = "";

    public string Description { get; set; } = "";
}

/// <summary>
/// System-level audit log: who touched which row, and when.
///
/// Deliberately has no foreign key, no soft-delete flag and no query filter: it
/// must outlive the row it describes, which is the entire point. An applicant may
/// be withdrawn — this trail keeps growing rather than vanishing with it.
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
/// A user who can sign in — a caseworker, supervisor or administrator.
///
/// Carries a <see cref="Role"/>: authorisation is no longer a stub. The role is
/// stamped into the bearer token (see Auth/TokenIssuer) and enforced by the
/// applicant endpoints through the policies in Auth/Policies.cs — a Viewer can
/// read but not change records, an Officer can create and update them, and an
/// Admin can additionally withdraw and restore.
/// </summary>
public class ApplicationUser
{
    public int Id { get; set; }

    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string FieldOffice { get; set; } = "";

    /// <summary>
    /// What this user may do. Defaults to <see cref="OfficerRole.Officer"/> — the
    /// everyday caseworker level — so a hand-inserted account without an explicit
    /// role can still do its job but cannot withdraw records.
    /// </summary>
    public OfficerRole Role { get; set; } = OfficerRole.Officer;

    /// <summary>PBKDF2, via ASP.NET's <c>IPasswordHasher</c>. Never a plaintext password.</summary>
    public string PasswordHash { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
