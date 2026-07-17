using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Naturalization.Api.Tests;

/// <summary>Helpers for talking to the API the way the SPA does.</summary>
public static class TestClient
{
    // The demo accounts seeded by DbInitializer.SeedUsersAsync — always present,
    // because account seeding is infrastructure and runs even with Seed:Demo off.
    // One per role, so the authorization tests can sign in as each.
    public const string OfficerEmail = "a.hernandez@example.gov";   // Admin
    public const string OfficerName = "A. Hernandez";
    public const string OfficerPassword = "Naturalize!Demo1";

    public const string AdminEmail = "a.hernandez@example.gov";
    public const string PlainOfficerEmail = "m.whitfield@example.gov";
    public const string ViewerEmail = "r.okafor@example.gov";
    // Every seeded demo account shares the one public demo password.
    public const string DemoPassword = OfficerPassword;

    /// <summary>Sign in as the default Admin officer and carry the bearer token.</summary>
    public static Task<HttpClient> SignedInAsync(this ApiFactory factory) =>
        factory.SignedInAsAsync(OfficerEmail);

    /// <summary>Sign in as a specific seeded officer and carry the bearer token.</summary>
    public static async Task<HttpClient> SignedInAsAsync(this ApiFactory factory, string email)
    {
        var client = factory.CreateClient();
        var token = await factory.TokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<string> TokenAsync(
        this ApiFactory factory, HttpClient? client = null, string email = OfficerEmail)
    {
        client ??= factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = DemoPassword });
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    public static object SampleApplicant(string alienNumber, string name = "Test Applicant")
    {
        // Callers pass a two-token display name; split it into the parts the API now
        // takes. Middle name is omitted (it's optional server-side). Residence
        // (localityId) is left null so the sample needs no seeded Locality — a real
        // FK now, unlike the country code, which is free text with no FK.
        var parts = name.Split(' ', 2);
        return new
        {
            alienNumber,
            naturalizationNumber = "",
            petitionNumber = "NBC2024000001",
            firstName = parts[0],
            lastName = parts.Length > 1 ? parts[1] : "Applicant",
            birthDate = "1988-03-14",
            admissionDate = "2016-06-01",
            address1 = "1 Test St",
            localityId = (int?)null,
            countryCode = "001",
            email = "test@example.com",
            status = "Received",
        };
    }

    public record LoginResponse(string AccessToken, DateTime ExpiresAt, OfficerResponse Officer);
    public record OfficerResponse(int Id, string Name, string Email, string FieldOffice, string Role);
    public record ApplicantResponse(int Id, string AlienNumber, string FullName);
    public record AuditEventResponse(int Id, string Action, string Actor, string Summary);
}
