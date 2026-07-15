using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;

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
        var client = await factory.SignedInAsync();

        /*
         * This build has no POST /api/cases — the case-write surface lives on the
         * enhancement branch. But the case DOMAIN and the Case Record PDF stay, so
         * the fixture is seeded straight through the DbContext. (Seed:Demo is off,
         * so nothing else is in the database.)
         */
        int caseId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NaturalizationDbContext>();
            var applicant = new Applicant
            {
                AlienNumber = "A900800700",
                FirstName = "Case",
                LastName = "Owner",
                DateOfBirth = new DateOnly(1988, 3, 14),
                LawfulPermanentResidentSince = new DateOnly(2016, 6, 1),
                CreatedAt = DateTime.UtcNow,
            };
            var record = new NaturalizationCase
            {
                Applicant = applicant,
                ReceiptNumber = "NBC2024000999",
                FiledOn = new DateOnly(2025, 1, 15),
                FieldOffice = "Boston, MA",
                Status = CaseStatus.InterviewCompleted,
            };
            record.Events.Add(new CaseEvent
            {
                EventType = "Application received",
                OccurredAt = DateTime.UtcNow,
                Actor = "System",
                Notes = "Seeded for the report test.",
            });
            db.Cases.Add(record);
            await db.SaveChangesAsync();
            caseId = record.Id;
        }

        var pdf = await client.GetAsync($"/api/reports/case/{caseId}.pdf");
        pdf.EnsureSuccessStatusCode();
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Mailing_labels_require_a_token()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/reports/labels.pdf");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Mailing_labels_render_a_valid_pdf_for_seeded_applicants()
    {
        var client = await factory.SignedInAsync();

        // Seed a few applicants directly (Seed:Demo is off, POST /api/applicants
        // exists but this keeps the fixture self-contained). Kept separate from the
        // shared ReportRoutes theory: with an empty table the labels report renders
        // a single "no applicants" page whose size the >1000-byte floor can't promise.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NaturalizationDbContext>();
            for (var i = 0; i < 3; i++)
            {
                db.Applicants.Add(new Applicant
                {
                    AlienNumber = $"A7008006{i:D2}",
                    FirstName = "Label",
                    MiddleName = i == 0 ? "Quincy" : null,
                    LastName = $"Recipient{i}",
                    AddressLine = $"{100 + i} Test St",
                    City = "Boston",
                    State = "MA",
                    PostalCode = "02101",
                    DateOfBirth = new DateOnly(1988, 3, 14),
                    LawfulPermanentResidentSince = new DateOnly(2016, 6, 1),
                    CreatedAt = DateTime.UtcNow,
                });
            }
            await db.SaveChangesAsync();
        }

        var res = await client.GetAsync("/api/reports/labels.pdf");
        res.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);

        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 1000, $"labels produced only {bytes.Length} bytes");
        Assert.NotNull(res.Content.Headers.ContentDisposition);
    }
}
