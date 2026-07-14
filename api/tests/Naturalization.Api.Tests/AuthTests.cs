using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;

namespace Naturalization.Api.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Protected_endpoint_without_a_token_is_401()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/applicants");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Health_is_open()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/health");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Correct_credentials_return_a_token()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = TestClient.OfficerEmail, password = TestClient.OfficerPassword });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<TestClient.LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal(TestClient.OfficerName, body.Officer.Name);
    }

    [Fact]
    public async Task Wrong_password_is_401()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = TestClient.OfficerEmail, password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_email_is_the_same_401_as_a_wrong_password()
    {
        // The two must be indistinguishable, or the endpoint becomes an oracle for
        // which addresses are real officer accounts.
        var client = factory.CreateClient();

        var unknown = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@example.gov", password = TestClient.OfficerPassword });
        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login",
            new { email = TestClient.OfficerEmail, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
    }

    [Fact]
    public async Task A_valid_token_opens_the_protected_endpoint()
    {
        var client = await factory.SignedInAsync();
        var res = await client.GetAsync("/api/applicants");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Me_reports_the_officer_from_the_token()
    {
        var client = await factory.SignedInAsync();
        var me = await client.GetFromJsonAsync<TestClient.OfficerResponse>("/api/auth/me");
        Assert.Equal(TestClient.OfficerEmail, me!.Email);
    }
}
