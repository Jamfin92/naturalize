using Naturalization.Api.Domain;

namespace Naturalization.Api.Dtos;

public record ApplicantDto(
    int Id,
    string AlienNumber,
    string FullName,
    DateOnly DateOfBirth,
    string CountryOfBirth,
    string Nationality,
    string AddressLine,
    string City,
    string State,
    string PostalCode,
    string Email,
    string Phone,
    DateOnly LawfulPermanentResidentSince,
    DateTime CreatedAt)
{
    public static ApplicantDto From(Applicant a) => new(
        a.Id, a.AlienNumber, a.FullName, a.DateOfBirth, a.CountryOfBirth, a.Nationality,
        a.AddressLine, a.City, a.State, a.PostalCode, a.Email, a.Phone,
        a.LawfulPermanentResidentSince, a.CreatedAt);
}

public record ApplicantInput(
    string AlienNumber,
    string FullName,
    DateOnly DateOfBirth,
    string CountryOfBirth,
    string Nationality,
    string AddressLine,
    string City,
    string State,
    string PostalCode,
    string Email,
    string Phone,
    DateOnly LawfulPermanentResidentSince);

public record CaseDto(
    int Id,
    int ApplicantId,
    string ApplicantName,
    string AlienNumber,
    string ReceiptNumber,
    DateOnly FiledOn,
    string FieldOffice,
    string Status,
    DateOnly? BiometricsOn,
    DateOnly? InterviewOn,
    DateOnly? OathOn,
    int DaysPending)
{
    public static CaseDto From(NaturalizationCase c, DateOnly today) => new(
        c.Id, c.ApplicantId, c.Applicant?.FullName ?? "", c.Applicant?.AlienNumber ?? "",
        c.ReceiptNumber, c.FiledOn, c.FieldOffice, c.Status.ToString(),
        c.BiometricsOn, c.InterviewOn, c.OathOn,
        today.DayNumber - c.FiledOn.DayNumber);
}

public record CaseDetailDto(
    int Id,
    int ApplicantId,
    string ApplicantName,
    string AlienNumber,
    string ReceiptNumber,
    DateOnly FiledOn,
    string FieldOffice,
    string Status,
    DateOnly? BiometricsOn,
    DateOnly? InterviewOn,
    DateOnly? OathOn,
    int DaysPending,
    ApplicantDto Applicant,
    IEnumerable<DocumentDto> Documents,
    IEnumerable<CaseEventDto> Events,
    DecisionDto? Decision,
    IEnumerable<string> AllowedTransitions);

public record CaseInput(int ApplicantId, string ReceiptNumber, DateOnly FiledOn, string FieldOffice);

public record TransitionInput(string Status, string Actor, string? Notes);

public record DecisionDto(
    int Id,
    int CaseId,
    string ReceiptNumber,
    string ApplicantName,
    string Outcome,
    DateOnly DecidedOn,
    string DecidedBy,
    string Rationale,
    string? DenialReasonCode)
{
    public static DecisionDto From(Decision d) => new(
        d.Id, d.CaseId,
        d.Case?.ReceiptNumber ?? "",
        d.Case?.Applicant?.FullName ?? "",
        d.Outcome.ToString(), d.DecidedOn, d.DecidedBy, d.Rationale, d.DenialReasonCode);
}

public record DecisionInput(
    int CaseId,
    string Outcome,
    string DecidedBy,
    string Rationale,
    string? DenialReasonCode);

public record DocumentDto(
    int Id,
    int CaseId,
    string DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string Status,
    DateTime UploadedAt)
{
    public static DocumentDto From(EvidenceDocument d) => new(
        d.Id, d.CaseId, d.DocumentType, d.FileName, d.ContentType, d.SizeBytes,
        d.Sha256, d.Status.ToString(), d.UploadedAt);
}

public record DocumentInput(int CaseId, string DocumentType, string FileName, string ContentType, long SizeBytes);

public record CaseEventDto(
    int Id,
    int CaseId,
    string EventType,
    DateTime OccurredAt,
    string Actor,
    string Notes)
{
    public static CaseEventDto From(CaseEvent e) => new(
        e.Id, e.CaseId, e.EventType, e.OccurredAt, e.Actor, e.Notes);
}

public record StatusCountDto(string Status, int Count);

public record MetricsDto(
    int TotalApplicants,
    int PendingCases,
    int ApprovedThisMonth,
    int DeniedThisMonth,
    int MedianDaysToDecision,
    IEnumerable<StatusCountDto> StatusCounts,
    IEnumerable<CaseEventDto> RecentEvents);

public record Paged<T>(IEnumerable<T> Items, int Page, int PageSize, int Total);
