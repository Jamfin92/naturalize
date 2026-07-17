using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;
using Naturalization.Api.Services;

namespace Naturalization.Api.Reports;

public partial class MigraDocReportGenerator(NaturalizationDbContext db, ApplicantMetrics metrics) : IReportGenerator
{
    public async Task<byte[]> ApprovalsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        // Decided applicants in the window: anyone with a decision date in range.
        var decided = await db.Applicants.AsNoTracking()
            .Where(a => a.DecisionDate != null && a.DecisionDate >= from && a.DecisionDate <= to)
            .OrderByDescending(a => a.DecisionDate)
            .ToListAsync(ct);

        var doc = ReportTheme.NewDocument(
            "Approvals Report",
            $"{from:MMM d, yyyy} – {to:MMM d, yyyy}");

        var section = doc.LastSection;

        // --- Totals -----------------------------------------------------------
        section.AddParagraph("Summary").Style = "SectionHeading";

        var approved = decided.Count(a => a.Status is ApplicationStatus.Approved or ApplicationStatus.Naturalized);
        var denied = decided.Count(a => a.Status == ApplicationStatus.Denied);
        var rate = decided.Count == 0 ? 0 : 100.0 * approved / decided.Count;

        var totals = ReportTheme.AddFieldGrid(section, 4);
        var t1 = totals.AddRow();
        ReportTheme.AddField(t1.Cells[0], "Decisions", decided.Count.ToString());
        ReportTheme.AddField(t1.Cells[1], "Approved", approved.ToString());
        ReportTheme.AddField(t1.Cells[2], "Denied", denied.ToString());
        ReportTheme.AddField(t1.Cells[3], "Approval rate", $"{rate:F1}%");

        // --- Register ---------------------------------------------------------
        section.AddParagraph("Decision register").Style = "SectionHeading";

        if (decided.Count == 0)
        {
            section.AddParagraph("No decisions were recorded in this period.")
                .Format.Font.Color = ReportTheme.Muted;
        }
        else
        {
            var table = ReportTheme.AddDataTable(section,
                ("Decided", 2.6), ("Applicant", 4.6), ("A-Number", 3.0), ("Status", 2.6), ("Notes", 3.8));

            var i = 0;
            foreach (var a in decided)
            {
                var row = ReportTheme.AddZebraRow(table, i++);
                ReportTheme.SetCell(row, 0, a.DecisionDate?.ToString("MMM d, yyyy") ?? "—");
                ReportTheme.SetCell(row, 1, a.FullName);
                ReportTheme.SetCell(row, 2, a.AlienNumber);
                ReportTheme.SetCell(row, 3, ReportTheme.StatusLabel(a.Status.ToString()), bold: true,
                    color: ReportTheme.StatusColor(a.Status.ToString()));
                ReportTheme.SetCell(row, 4, a.DecisionNotes ?? "—");
            }
        }

        return Render(doc);
    }

    public async Task<byte[]> PipelineAsync(CancellationToken ct = default)
    {
        var m = await metrics.ComputeAsync();
        var oldest = await metrics.OldestPendingAsync(15);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var doc = ReportTheme.NewDocument(
            "Pipeline Report",
            $"Caseload as at {today:MMM d, yyyy}");

        var section = doc.LastSection;

        // --- Headline ---------------------------------------------------------
        section.AddParagraph("Caseload").Style = "SectionHeading";

        var totals = ReportTheme.AddFieldGrid(section, 4);
        var t1 = totals.AddRow();
        ReportTheme.AddField(t1.Cells[0], "Applicants on file", m.TotalApplicants.ToString());
        ReportTheme.AddField(t1.Cells[1], "Pending", m.Pending.ToString());
        ReportTheme.AddField(t1.Cells[2], "Approved this month", m.ApprovedThisMonth.ToString());
        ReportTheme.AddField(t1.Cells[3], "Denied this month", m.DeniedThisMonth.ToString());

        // --- By status, with an inline bar ------------------------------------
        section.AddParagraph("By status").Style = "SectionHeading";

        var counts = m.StatusCounts.ToList();
        var max = Math.Max(1, counts.Max(x => x.Count));

        var statusTable = ReportTheme.AddDataTable(section,
            ("Status", 5.2), ("Applicants", 1.8), ("Share", 9.6));

        var i = 0;
        foreach (var s in counts)
        {
            var row = ReportTheme.AddZebraRow(statusTable, i++);
            ReportTheme.SetCell(row, 0, ReportTheme.StatusLabel(s.Status.ToString()), bold: true,
                color: ReportTheme.StatusColor(s.Status.ToString()));
            ReportTheme.SetCell(row, 1, s.Count.ToString());
            ReportTheme.AddBar(row.Cells[2], s.Count, max, maxWidthCm: 8.8);
        }

        // --- Aging ------------------------------------------------------------
        section.AddParagraph("Oldest pending applicants").Style = "SectionHeading";

        if (oldest.Count == 0)
        {
            section.AddParagraph("No applicants are currently pending.").Format.Font.Color =
                ReportTheme.Muted;
        }
        else
        {
            section.AddParagraph(
                "Records that have been open longest, oldest first. Anything past roughly a year " +
                "warrants a look.").Format.Font.Color = ReportTheme.Muted;

            var table = ReportTheme.AddDataTable(section,
                ("A-Number", 3.0), ("Applicant", 5.0), ("Status", 3.4), ("Added", 2.8), ("Days", 1.6));

            var j = 0;
            foreach (var a in oldest)
            {
                var added = DateOnly.FromDateTime(a.CreatedAt);
                var days = today.DayNumber - added.DayNumber;
                var row = ReportTheme.AddZebraRow(table, j++);
                ReportTheme.SetCell(row, 0, a.AlienNumber);
                ReportTheme.SetCell(row, 1, a.FullName);
                ReportTheme.SetCell(row, 2, ReportTheme.StatusLabel(a.Status.ToString()));
                ReportTheme.SetCell(row, 3, added.ToString("MMM d, yyyy"));

                // Past a year, flag it red — that is the number a supervisor is looking for.
                ReportTheme.SetCell(row, 4, days.ToString(), bold: days > 365,
                    color: days > 365 ? ReportTheme.Red : null);
            }
        }

        return Render(doc);
    }

    private static byte[] Render(Document doc)
    {
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();

        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, closeStream: false);
        return ms.ToArray();
    }
}
