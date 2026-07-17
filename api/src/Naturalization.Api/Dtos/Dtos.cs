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

/// <summary>A code/description pair from one of the lookup tables.</summary>
public record LookupDto(string Code, string Description)
{
    public static LookupDto From(TownCode t) => new(t.Code, t.Description);
    public static LookupDto From(CountryCode c) => new(c.Code, c.Description);
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
    string TownCode,
    string CountryCode,
    string ZipCode,
    string Email,
    string Status,
    DateOnly? DecisionDate,
    string DecisionNotes,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static ApplicantDto From(Applicant a) => new(
        a.Id, a.AlienNumber, a.NaturalizationNumber, a.PetitionNumber,
        a.FirstName, a.MiddleName ?? "", a.LastName, a.FullName,
        a.BirthDate, a.AdmissionDate,
        a.Address1, a.TownCode, a.CountryCode, a.ZipCode, a.Email,
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
    string TownCode,
    string CountryCode,
    string ZipCode,
    string Email,
    // Status comes over the wire as the enum name; blank defaults to Received.
    string? Status,
    DateOnly? DecisionDate,
    string? DecisionNotes);

public record Paged<T>(IEnumerable<T> Items, int Page, int PageSize, int Total);
