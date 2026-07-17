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
                    Address1 = $"{100 + i} Test St",
                    TownCode = "001",
                    CountryCode = "001",
                    ZipCode = "02101",
                    BirthDate = new DateOnly(1988, 3, 14),
                    AdmissionDate = new DateOnly(2016, 6, 1),
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

    [Fact]
    public async Task Mailing_labels_filter_by_date_added_and_name_the_download()
    {
        var client = await factory.SignedInAsync();

        // Two applicants added on distinct, fixed days so the CreatedAt filter has
        // something to include and exclude.
        var early = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);
        var late = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NaturalizationDbContext>();
            db.Applicants.Add(new Applicant
            {
                AlienNumber = "A710000001",
                FirstName = "Early",
                LastName = "Arrival",
                Address1 = "1 Winter Way",
                TownCode = "001",
                CountryCode = "001",
                ZipCode = "02101",
                BirthDate = new DateOnly(1988, 3, 14),
                AdmissionDate = new DateOnly(2016, 6, 1),
                CreatedAt = early,
            });
            db.Applicants.Add(new Applicant
            {
                AlienNumber = "A710000002",
                FirstName = "Late",
                LastName = "Arrival",
                Address1 = "2 Spring St",
                TownCode = "001",
                CountryCode = "001",
                ZipCode = "02101",
                BirthDate = new DateOnly(1990, 5, 2),
                AdmissionDate = new DateOnly(2017, 7, 1),
                CreatedAt = late,
            });
            await db.SaveChangesAsync();
        }

        // A single date names the file for that day.
        var single = await client.GetAsync("/api/reports/labels.pdf?from=2026-01-10&to=2026-01-10");
        single.EnsureSuccessStatusCode();
        Assert.Equal("mailing-labels-20260110.pdf", single.Content.Headers.ContentDisposition?.FileNameStar
            ?? single.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        // A range names the file for both bounds.
        var range = await client.GetAsync("/api/reports/labels.pdf?from=2026-01-01&to=2026-02-01");
        range.EnsureSuccessStatusCode();
        Assert.Equal("mailing-labels-20260101-20260201.pdf",
            range.Content.Headers.ContentDisposition?.FileNameStar
            ?? range.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var rangeBytes = await range.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(rangeBytes, 0, 4));
    }

    [Fact]
    public async Task Mailing_labels_reject_a_backwards_range()
    {
        var client = await factory.SignedInAsync();
        var res = await client.GetAsync("/api/reports/labels.pdf?from=2026-03-01&to=2026-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
