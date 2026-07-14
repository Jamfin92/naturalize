using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Naturalization.Api.Tests;

public class ReportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static TheoryData<string> ReportRoutes => new()
    {
        "/api/reports/pipeline.pdf",
        "/api/reports/approvals.pdf",
    };

    [Theory]
    [MemberData(nameof(ReportRoutes))]
    public async Task Reports_require_a_token(string route)
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ReportRoutes))]
    public async Task Reports_render_a_valid_pdf(string route)
    {
        var client = await factory.SignedInAsync();

        var res = await client.GetAsync(route);
        res.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);

        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000, $"{route} produced only {bytes.Length} bytes");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));

        // The client relies on this header to name the download; CORS exposes it.
        Assert.NotNull(res.Content.Headers.ContentDisposition);
    }

    [Fact]
    public async Task A_case_record_renders_for_a_seeded_case()
    {
        // This test seeds one applicant + case of its own, since Seed:Demo is off.
        var client = await factory.SignedInAsync();

        var created = await client.PostAsJsonAsync("/api/applicants",
            TestClient.SampleApplicant("A900800700", "Case Owner"));
        var applicant = await created.Content.ReadFromJsonAsync<TestClient.ApplicantResponse>();

        var caseRes = await client.PostAsJsonAsync("/api/cases", new
        {
            applicantId = applicant!.Id,
            receiptNumber = "NBC2024000999",
            filedOn = "2025-01-15",
            fieldOffice = "Boston, MA",
        });
        caseRes.EnsureSuccessStatusCode();
        var created2 = await caseRes.Content.ReadFromJsonAsync<CaseResponse>();

        var pdf = await client.GetAsync($"/api/reports/case/{created2!.Id}.pdf");
        pdf.EnsureSuccessStatusCode();
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    private record CaseResponse(int Id, string ReceiptNumber);
}
