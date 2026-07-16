using System.Net;
using System.Net.Http.Json;

namespace Naturalization.Api.Tests;

/// <summary>
/// The role gates on the applicant mutations. A Viewer can read but not change
/// anything; an Officer can add and edit applicants but not withdraw them; an
/// Admin can do everything. Enforcement is by authorization policy, so the wrong
/// role gets a 403 — not a 401 (which would mean "not signed in") and not a
/// silent success.
/// </summary>
public class AuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_reports_the_officers_role()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = TestClient.ViewerEmail, password = TestClient.DemoPassword });
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<TestClient.LoginResponse>();
        Assert.Equal("Viewer", body!.Officer.Role);
    }

    [Fact]
    public async Task A_viewer_can_read_the_register()
    {
        var viewer = await factory.SignedInAsAsync(TestClient.ViewerEmail);
        var res = await viewer.GetAsync("/api/applicants");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_viewer_cannot_create_an_applicant()
    {
        var viewer = await factory.SignedInAsAsync(TestClient.ViewerEmail);
        var res = await viewer.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A710000001", "Viewer Create"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task A_viewer_cannot_update_an_applicant()
    {
        // Seed the record as the Admin, then try to edit it as the Viewer.
        var admin = await factory.SignedInAsAsync(TestClient.AdminEmail);
        var created = await admin.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A710000002", "Viewer Update"));
        var applicant = await created.Content.ReadFromJsonAsync<TestClient.ApplicantResponse>();

        var viewer = await factory.SignedInAsAsync(TestClient.ViewerEmail);
        var res = await viewer.PutAsJsonAsync($"/api/applicants/{applicant!.Id}",
            TestClient.SampleApplicant("A710000002", "Viewer Edited"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task A_viewer_cannot_withdraw_an_applicant()
    {
        var admin = await factory.SignedInAsAsync(TestClient.AdminEmail);
        var created = await admin.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A710000003", "Viewer Withdraw"));
        var applicant = await created.Content.ReadFromJsonAsync<TestClient.ApplicantResponse>();

        var viewer = await factory.SignedInAsAsync(TestClient.ViewerEmail);
        var res = await viewer.DeleteAsync($"/api/applicants/{applicant!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task An_officer_can_create_and_update_but_not_withdraw()
    {
        var officer = await factory.SignedInAsAsync(TestClient.PlainOfficerEmail);

        // Create: allowed.
        var created = await officer.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A710000004", "Officer Person"));
        created.EnsureSuccessStatusCode();
        var applicant = await created.Content.ReadFromJsonAsync<TestClient.ApplicantResponse>();

        // Update: allowed.
        var updated = await officer.PutAsJsonAsync($"/api/applicants/{applicant!.Id}",
            TestClient.SampleApplicant("A710000004", "Officer Edited"));
        updated.EnsureSuccessStatusCode();

        // Withdraw: not allowed — that is Admin-only.
        var withdrawn = await officer.DeleteAsync($"/api/applicants/{applicant.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, withdrawn.StatusCode);
    }

    [Fact]
    public async Task An_admin_can_withdraw_and_restore()
    {
        var admin = await factory.SignedInAsAsync(TestClient.AdminEmail);

        var created = await admin.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A710000005", "Admin Person"));
        var applicant = await created.Content.ReadFromJsonAsync<TestClient.ApplicantResponse>();

        var withdrawn = await admin.DeleteAsync($"/api/applicants/{applicant!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);

        var restored = await admin.PostAsync($"/api/applicants/{applicant.Id}/restore", null);
        restored.EnsureSuccessStatusCode();
    }
}
