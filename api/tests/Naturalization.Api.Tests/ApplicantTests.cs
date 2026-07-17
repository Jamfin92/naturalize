using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;

namespace Naturalization.Api.Tests;

public class ApplicantTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Creating_an_applicant_writes_an_audit_row_naming_the_token_officer()
    {
        var client = await factory.SignedInAsync();

        var created = await client.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A100200300", "Auditable Person"));
        created.EnsureSuccessStatusCode();
        var applicant = await created.Content.ReadFromJsonAsync<TestClient.ApplicantResponse>();

        var history = await client.GetFromJsonAsync<List<TestClient.AuditEventResponse>>(
            $"/api/applicants/{applicant!.Id}/history");

        var creation = Assert.Single(history!);
        Assert.Equal("Created", creation.Action);

        // The actor came from the bearer token's claims, NOT from the request body.
        // If MapInboundClaims were left on, this would read "Unknown officer" — the
        // regression this whole assertion exists to catch.
        Assert.Equal(TestClient.OfficerName, creation.Actor);
    }

    [Fact]
    public async Task Updating_an_applicant_records_which_fields_changed()
    {
        var client = await factory.SignedInAsync();

        // A locality to move the applicant to — residence is a FK now, so the
        // target row must exist. The sample applicant starts with none.
        int localityId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NaturalizationDbContext>();
            var loc = new Locality { ZipCode = "02139", Name = "Cambridge", State = "MA" };
            db.Localities.Add(loc);
            await db.SaveChangesAsync();
            localityId = loc.Id;
        }

        var created = await client.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A200300400", "Editable Person"));
        var applicant = await created.Content.ReadFromJsonAsync<TestClient.ApplicantResponse>();

        var updated = await client.PutAsJsonAsync($"/api/applicants/{applicant!.Id}", new
        {
            alienNumber = "A200300400",
            naturalizationNumber = "",
            petitionNumber = "NBC2024000001",
            firstName = "Editable",
            lastName = "Person",
            birthDate = "1988-03-14",
            admissionDate = "2016-06-01",
            address1 = "1 Test St",
            localityId, // moved from no locality to Cambridge
            countryCode = "001",
            email = "test@example.com",
            status = "Received",
        });
        updated.EnsureSuccessStatusCode();

        var history = await client.GetFromJsonAsync<List<TestClient.AuditEventResponse>>(
            $"/api/applicants/{applicant.Id}/history");

        Assert.Contains(history!, e => e.Action == "Updated" && e.Summary.Contains("residence"));
    }

    [Fact]
    public async Task Withdrawing_hides_the_applicant_but_keeps_the_row_and_its_trail()
    {
        var client = await factory.SignedInAsync();

        var created = await client.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A300400500", "Withdrawn Person"));
        var applicant = await created.Content.ReadFromJsonAsync<TestClient.ApplicantResponse>();
        var id = applicant!.Id;

        var deleted = await client.DeleteAsync($"/api/applicants/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // Hidden from the read path.
        var afterGet = await client.GetAsync($"/api/applicants/{id}");
        Assert.Equal(HttpStatusCode.NotFound, afterGet.StatusCode);

        // But the row and its audit trail are still in the database. Assert against
        // the DbContext with IgnoreQueryFilters — the whole point of a soft delete
        // is that this is still true.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NaturalizationDbContext>();

        var row = await db.Applicants.IgnoreQueryFilters().SingleAsync(a => a.Id == id);
        Assert.True(row.IsDeleted);
        Assert.Equal(TestClient.OfficerName, row.DeletedBy);

        var trail = await db.AuditEvents
            .Where(e => e.EntityType == nameof(Applicant) && e.EntityId == id)
            .ToListAsync();
        Assert.Contains(trail, e => e.Action == "Created");
        Assert.Contains(trail, e => e.Action == "Deleted");
    }

    [Fact]
    public async Task A_withdrawn_applicant_can_be_restored()
    {
        var client = await factory.SignedInAsync();

        var created = await client.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A400500600", "Restore Me"));
        var applicant = await created.Content.ReadFromJsonAsync<TestClient.ApplicantResponse>();

        await client.DeleteAsync($"/api/applicants/{applicant!.Id}");
        var restore = await client.PostAsync($"/api/applicants/{applicant.Id}/restore", null);
        restore.EnsureSuccessStatusCode();

        var afterGet = await client.GetAsync($"/api/applicants/{applicant.Id}");
        afterGet.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reusing_a_withdrawn_applicants_A_Number_is_a_409_not_a_500()
    {
        // The money test. The A-Number's unique index is unfiltered, so a naive
        // collision check misses the soft-deleted row and lets the insert hit the
        // index — a raw DbUpdateException 500. The endpoint probes with
        // IgnoreQueryFilters and returns an actionable conflict instead.
        var client = await factory.SignedInAsync();

        var created = await client.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A500600700", "Original Person"));
        var applicant = await created.Content.ReadFromJsonAsync<TestClient.ApplicantResponse>();
        await client.DeleteAsync($"/api/applicants/{applicant!.Id}");

        var clash = await client.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A500600700", "Impostor Person"));

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var message = await clash.Content.ReadAsStringAsync();
        Assert.Contains("withdrawn applicant", message);
    }

    [Fact]
    public async Task A_live_duplicate_A_Number_is_a_validation_error_not_a_conflict()
    {
        var client = await factory.SignedInAsync();

        await client.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A600700800", "First Person"));
        var second = await client.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A600700800", "Second Person"));

        // A live collision is a plain 400 — there is no withdrawn record to restore.
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }
}
