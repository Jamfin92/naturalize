using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Naturalization.Api.Tests;

/// <summary>Helpers for talking to the API the way the SPA does.</summary>
public static class TestClient
{
    // The demo officer seeded by DbInitializer.SeedOfficersAsync — always present,
    // because officer seeding is infrastructure and runs even with Seed:Demo off.
    public const string OfficerEmail = "a.hernandez@example.gov";
    public const string OfficerName = "A. Hernandez";
    public const string OfficerPassword = "Naturalize!Demo1";

    /// <summary>Sign in and return a client that carries the bearer token.</summary>
    public static async Task<HttpClient> SignedInAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.TokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<string> TokenAsync(this ApiFactory factory, HttpClient? client = null)
    {
        client ??= factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = OfficerEmail, password = OfficerPassword });
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    public static object SampleApplicant(string alienNumber, string fullName = "Test Applicant") => new
    {
        alienNumber,
        fullName,
        dateOfBirth = "1988-03-14",
        countryOfBirth = "Kenya",
        nationality = "Kenyan",
        addressLine = "1 Test St",
        city = "Boston",
        state = "MA",
        postalCode = "02101",
        email = "test@example.com",
        phone = "(555) 555-5555",
        lawfulPermanentResidentSince = "2016-06-01",
    };

    public record LoginResponse(string AccessToken, DateTime ExpiresAt, OfficerResponse Officer);
    public record OfficerResponse(int Id, string Name, string Email, string FieldOffice);
    public record ApplicantResponse(int Id, string AlienNumber, string FullName);
    public record AuditEventResponse(int Id, string Action, string Actor, string Summary);
}
