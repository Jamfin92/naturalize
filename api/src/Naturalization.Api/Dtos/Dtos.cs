using Naturalization.Api.Domain;

namespace Naturalization.Api.Dtos;

public record LoginInput(string Email, string Password);

public record OfficerDto(int Id, string Name, string Email, string FieldOffice, string Role)
{
    public static OfficerDto From(ApplicationUser u) =>
        new(u.Id, u.FullName, u.Email, u.FieldOffice, u.Role.ToString());
}

public record LoginResult(string AccessToken, DateTime ExpiresAt, OfficerDto Officer);

public record AuditEventDto(
    int Id,
    string EntityType,
    int EntityId,
    string Action,
    string Actor,
    DateTime OccurredAt,
    string Summary)
{
    public static AuditEventDto From(AuditEvent e) => new(
        e.Id, e.EntityType, e.EntityId, e.Action, e.Actor, e.OccurredAt, e.Summary);
}

/// <summary>A code/description pair from the country lookup.</summary>
public record LookupDto(string Code, string Description)
{
    public static LookupDto From(CountryCode c) => new(c.Code, c.Description);
}

/// <summary>A residential locality: ZIP, city/town name and state, keyed by Id.</summary>
public record LocalityDto(int Id, string ZipCode, string Name, string State)
{
    public static LocalityDto From(Locality l) => new(l.Id, l.ZipCode, l.Name, l.State);
}

public record ApplicantDto(
    int Id,
    string AlienNumber,
    string NaturalizationNumber,
    string PetitionNumber,
    string FirstName,
    string MiddleName,
    string LastName,
    // Computed "First Middle Last", kept on the DTO so the register, detail
    // header and toasts can render one display string without re-joining parts.
    string FullName,
    DateOnly BirthDate,
    DateOnly AdmissionDate,
    string Address1,
    int? LocalityId,
    // City / State / ZipCode are denormalised from the joined Locality for
    // display — the register header, detail card and mailing labels render them
    // without a second call. Read-only: create/edit send only LocalityId (see
    // ApplicantInput). Blank when the applicant has no locality resolved yet.
    string City,
    string State,
    string ZipCode,
    string CountryCode,
    string Email,
    string Status,
    DateOnly? DecisionDate,
    string DecisionNotes,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    // Reads a.Locality, so the query must Include it (endpoints do).
    public static ApplicantDto From(Applicant a) => new(
        a.Id, a.AlienNumber, a.NaturalizationNumber, a.PetitionNumber,
        a.FirstName, a.MiddleName ?? "", a.LastName, a.FullName,
        a.BirthDate, a.AdmissionDate,
        a.Address1, a.LocalityId,
        a.Locality?.Name ?? "", a.Locality?.State ?? "", a.Locality?.ZipCode ?? "",
        a.CountryCode, a.Email,
        a.Status.ToString(), a.DecisionDate, a.DecisionNotes ?? "",
        a.CreatedAt, a.UpdatedAt);
}

public record ApplicantInput(
    string AlienNumber,
    string? NaturalizationNumber,
    string? PetitionNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    DateOnly BirthDate,
    DateOnly AdmissionDate,
    string Address1,
    // Residence is the Locality FK; ZIP/city/state are read back from that row.
    int? LocalityId,
    string CountryCode,
    string Email,
    // Status comes over the wire as the enum name; blank defaults to Received.
    string? Status,
    DateOnly? DecisionDate,
    string? DecisionNotes);

public record Paged<T>(IEnumerable<T> Items, int Page, int PageSize, int Total);
