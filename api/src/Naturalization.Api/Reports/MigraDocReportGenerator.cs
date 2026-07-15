using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;
using Naturalization.Api.Services;

namespace Naturalization.Api.Reports;

public partial class MigraDocReportGenerator(NaturalizationDbContext db, CaseMetrics metrics) : IReportGenerator
{
    public async Task<byte[]> CaseRecordAsync(int caseId, CancellationToken ct = default)
    {
        var c = await db.Cases.AsNoTracking()
            .Include(x => x.Applicant)
            .Include(x => x.Documents)
            .Include(x => x.Events)
            .Include(x => x.Decision)
            .FirstOrDefaultAsync(x => x.Id == caseId, ct)
            ?? throw new KeyNotFoundException($"No case with id {caseId}.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var doc = ReportTheme.NewDocument(
            "Case Record",
            $"{c.Applicant.FullName}  ·  {c.ReceiptNumber}  ·  {c.FieldOffice}");

        var section = doc.LastSection;

        // --- Case summary -----------------------------------------------------
        section.AddParagraph("Case summary").Style = "SectionHeading";

        var summary = ReportTheme.AddFieldGrid(section, 4);
        var r1 = summary.AddRow();
        ReportTheme.AddField(r1.Cells[0], "Receipt number", c.ReceiptNumber);
        ReportTheme.AddField(r1.Cells[1], "Filed on", c.FiledOn.ToString("MMM d, yyyy"));
        ReportTheme.AddField(r1.Cells[2], "Field office", c.FieldOffice);
        ReportTheme.AddField(r1.Cells[3], "Days since filing", (today.DayNumber - c.FiledOn.DayNumber).ToString());

        var r2 = summary.AddRow();
        ReportTheme.AddField(r2.Cells[0], "Status", ReportTheme.StatusLabel(c.Status.ToString()));
        ReportTheme.AddField(r2.Cells[1], "Biometrics", c.BiometricsOn?.ToString("MMM d, yyyy") ?? "—");
        ReportTheme.AddField(r2.Cells[2], "Interview", c.InterviewOn?.ToString("MMM d, yyyy") ?? "—");
        ReportTheme.AddField(r2.Cells[3], "Oath", c.OathOn?.ToString("MMM d, yyyy") ?? "—");

        // --- Applicant --------------------------------------------------------
        section.AddParagraph("Applicant particulars").Style = "SectionHeading";
        var a = c.Applicant;

        var bio = ReportTheme.AddFieldGrid(section, 3);
        var b1 = bio.AddRow();
        ReportTheme.AddField(b1.Cells[0], "Full name", a.FullName);
        ReportTheme.AddField(b1.Cells[1], "A-Number", a.AlienNumber);
        ReportTheme.AddField(b1.Cells[2], "Date of birth", a.DateOfBirth.ToString("MMM d, yyyy"));

        var b2 = bio.AddRow();
        ReportTheme.AddField(b2.Cells[0], "Country of birth", a.CountryOfBirth);
        ReportTheme.AddField(b2.Cells[1], "Nationality", a.Nationality);
        ReportTheme.AddField(b2.Cells[2], "LPR since", a.LawfulPermanentResidentSince.ToString("MMM d, yyyy"));

        var b3 = bio.AddRow();
        ReportTheme.AddField(b3.Cells[0], "Residence", $"{a.AddressLine}, {a.City}, {a.State} {a.PostalCode}");
        ReportTheme.AddField(b3.Cells[1], "Email", a.Email);
        ReportTheme.AddField(b3.Cells[2], "Phone", a.Phone);

        // --- Decision ---------------------------------------------------------
        if (c.Decision is not null)
        {
            section.AddParagraph("Decision").Style = "SectionHeading";
            var d = c.Decision;

            var dt = ReportTheme.AddFieldGrid(section, 3);
            var d1 = dt.AddRow();
            ReportTheme.AddField(d1.Cells[0], "Outcome", d.Outcome.ToString());
            ReportTheme.AddField(d1.Cells[1], "Decided on", d.DecidedOn.ToString("MMM d, yyyy"));
            ReportTheme.AddField(d1.Cells[2], "Decided by", d.DecidedBy);

            if (d.DenialReasonCode is not null)
            {
                var p = section.AddParagraph();
                p.Style = "FieldLabel";
                p.AddText("STATUTORY BASIS");
                var code = section.AddParagraph(d.DenialReasonCode);
                code.Style = "FieldValue";
                code.Format.Font.Color = ReportTheme.Red;
                code.Format.Font.Bold = true;
            }

            var rl = section.AddParagraph("RATIONALE");
            rl.Style = "FieldLabel";
            var rv = section.AddParagraph(d.Rationale);
            rv.Style = "FieldValue";
        }

        // --- Evidence ---------------------------------------------------------
        section.AddParagraph("Evidence on file").Style = "SectionHeading";

        if (c.Documents.Count == 0)
        {
            section.AddParagraph("No evidence has been filed for this case.").Format.Font.Color =
                ReportTheme.Muted;
        }
        else
        {
            var table = ReportTheme.AddDataTable(section,
                ("Document type", 5.6), ("File name", 5.6), ("Size", 1.8), ("Status", 1.9), ("Filed", 2.1));

            var i = 0;
            foreach (var d in c.Documents.OrderBy(x => x.UploadedAt))
            {
                var row = ReportTheme.AddZebraRow(table, i++);
                ReportTheme.SetCell(row, 0, d.DocumentType);
                ReportTheme.SetCell(row, 1, d.FileName);
                ReportTheme.SetCell(row, 2, FormatBytes(d.SizeBytes));
                ReportTheme.SetCell(row, 3, d.Status.ToString(),
                    color: d.Status == DocumentStatus.Rejected ? ReportTheme.Red : null);
                ReportTheme.SetCell(row, 4, d.UploadedAt.ToString("MMM d, yyyy"));
            }
        }

        // --- Audit trail ------------------------------------------------------
        section.AddParagraph("Audit trail").Style = "SectionHeading";

        var events = ReportTheme.AddDataTable(section,
            ("Date", 2.6), ("Event", 5.4), ("Actor", 3.4), ("Notes", 5.6));

        var j = 0;
        foreach (var e in c.Events.OrderBy(x => x.OccurredAt))
        {
            var row = ReportTheme.AddZebraRow(events, j++);
            ReportTheme.SetCell(row, 0, e.OccurredAt.ToString("MMM d, yyyy"));
            ReportTheme.SetCell(row, 1, e.EventType, bold: true);
            ReportTheme.SetCell(row, 2, e.Actor);
            ReportTheme.SetCell(row, 3, e.Notes);
        }

        return Render(doc);
    }

    public async Task<byte[]> ApprovalsAsync(
        DateOnly from, DateOnly to, string? fieldOffice, CancellationToken ct = default)
    {
        var query = db.Decisions.AsNoTracking()
            .Include(d => d.Case).ThenInclude(c => c.Applicant)
            .Where(d => d.DecidedOn >= from && d.DecidedOn <= to);

        if (!string.IsNullOrWhiteSpace(fieldOffice))
            query = query.Where(d => d.Case.FieldOffice == fieldOffice);

        var decisions = await query.OrderByDescending(d => d.DecidedOn).ToListAsync(ct);

        var scope = string.IsNullOrWhiteSpace(fieldOffice) ? "All field offices" : fieldOffice;
        var doc = ReportTheme.NewDocument(
            "Approvals Report",
            $"{from:MMM d, yyyy} – {to:MMM d, yyyy}  ·  {scope}");

        var section = doc.LastSection;

        // --- Totals -----------------------------------------------------------
        section.AddParagraph("Summary").Style = "SectionHeading";

        var approved = decisions.Count(d => d.Outcome == DecisionOutcome.Approved);
        var denied = decisions.Count(d => d.Outcome == DecisionOutcome.Denied);
        var continued = decisions.Count(d => d.Outcome == DecisionOutcome.Continued);
        var rate = decisions.Count == 0 ? 0 : 100.0 * approved / decisions.Count;

        var totals = ReportTheme.AddFieldGrid(section, 4);
        var t1 = totals.AddRow();
        ReportTheme.AddField(t1.Cells[0], "Decisions", decisions.Count.ToString());
        ReportTheme.AddField(t1.Cells[1], "Approved", approved.ToString());
        ReportTheme.AddField(t1.Cells[2], "Denied", denied.ToString());
        ReportTheme.AddField(t1.Cells[3], "Approval rate", $"{rate:F1}%");

        if (continued > 0)
            section.AddParagraph($"{continued} case(s) continued pending further evidence.")
                .Format.Font.Color = ReportTheme.Muted;

        // --- By office --------------------------------------------------------
        section.AddParagraph("By field office").Style = "SectionHeading";

        var byOffice = decisions
            .GroupBy(d => d.Case.FieldOffice)
            .OrderByDescending(gr => gr.Count())
            .ToList();

        if (byOffice.Count == 0)
        {
            section.AddParagraph("No decisions were recorded in this period.")
                .Format.Font.Color = ReportTheme.Muted;
        }
        else
        {
            var officeTable = ReportTheme.AddDataTable(section,
                ("Field office", 7.0), ("Decisions", 2.4), ("Approved", 2.4), ("Denied", 2.4), ("Rate", 2.8));

            var i = 0;
            foreach (var gr in byOffice)
            {
                var ap = gr.Count(d => d.Outcome == DecisionOutcome.Approved);
                var dn = gr.Count(d => d.Outcome == DecisionOutcome.Denied);
                var pct = gr.Count() == 0 ? 0 : 100.0 * ap / gr.Count();

                var row = ReportTheme.AddZebraRow(officeTable, i++);
                ReportTheme.SetCell(row, 0, gr.Key, bold: true);
                ReportTheme.SetCell(row, 1, gr.Count().ToString());
                ReportTheme.SetCell(row, 2, ap.ToString(), color: ReportTheme.Green);
                ReportTheme.SetCell(row, 3, dn.ToString(), color: dn > 0 ? ReportTheme.Red : null);
                ReportTheme.SetCell(row, 4, $"{pct:F0}%");
            }
        }

        // --- Register ---------------------------------------------------------
        section.AddParagraph("Decision register").Style = "SectionHeading";

        if (decisions.Count == 0)
        {
            section.AddParagraph("Nothing to list.").Format.Font.Color = ReportTheme.Muted;
        }
        else
        {
            var table = ReportTheme.AddDataTable(section,
                ("Decided", 2.3), ("Applicant", 4.0), ("Receipt", 3.0), ("Outcome", 2.0),
                ("Officer", 3.2), ("Basis", 2.5));

            var i = 0;
            foreach (var d in decisions)
            {
                var row = ReportTheme.AddZebraRow(table, i++);
                ReportTheme.SetCell(row, 0, d.DecidedOn.ToString("MMM d, yyyy"));
                ReportTheme.SetCell(row, 1, d.Case.Applicant.FullName);
                ReportTheme.SetCell(row, 2, d.Case.ReceiptNumber);
                ReportTheme.SetCell(row, 3, d.Outcome.ToString(), bold: true,
                    color: ReportTheme.StatusColor(d.Outcome.ToString()));
                ReportTheme.SetCell(row, 4, d.DecidedBy);
                ReportTheme.SetCell(row, 5, d.DenialReasonCode ?? "—");
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
        ReportTheme.AddField(t1.Cells[1], "Cases pending", m.PendingCases.ToString());
        ReportTheme.AddField(t1.Cells[2], "Approved this month", m.ApprovedThisMonth.ToString());
        ReportTheme.AddField(t1.Cells[3], "Median days to decision", m.MedianDaysToDecision.ToString());

        // --- By status, with an inline bar ------------------------------------
        section.AddParagraph("By status").Style = "SectionHeading";

        var counts = m.StatusCounts.ToList();
        var max = Math.Max(1, counts.Max(x => x.Count));

        var statusTable = ReportTheme.AddDataTable(section,
            ("Status", 5.2), ("Cases", 1.8), ("Share", 9.6));

        var i = 0;
        foreach (var s in counts)
        {
            var row = ReportTheme.AddZebraRow(statusTable, i++);
            ReportTheme.SetCell(row, 0, ReportTheme.StatusLabel(s.Status), bold: true,
                color: ReportTheme.StatusColor(s.Status));
            ReportTheme.SetCell(row, 1, s.Count.ToString());
            ReportTheme.AddBar(row.Cells[2], s.Count, max, maxWidthCm: 8.8);
        }

        // --- Aging ------------------------------------------------------------
        section.AddParagraph("Oldest pending cases").Style = "SectionHeading";

        if (oldest.Count == 0)
        {
            section.AddParagraph("No cases are currently pending.").Format.Font.Color =
                ReportTheme.Muted;
        }
        else
        {
            section.AddParagraph(
                "Cases that have been open longest, oldest first. Anything past roughly a year " +
                "warrants a look.").Format.Font.Color = ReportTheme.Muted;

            var table = ReportTheme.AddDataTable(section,
                ("Receipt", 3.0), ("Applicant", 4.2), ("Field office", 3.6),
                ("Status", 3.4), ("Filed", 2.2), ("Days", 1.4));

            var j = 0;
            foreach (var c in oldest)
            {
                var days = today.DayNumber - c.FiledOn.DayNumber;
                var row = ReportTheme.AddZebraRow(table, j++);
                ReportTheme.SetCell(row, 0, c.ReceiptNumber);
                ReportTheme.SetCell(row, 1, c.Applicant.FullName);
                ReportTheme.SetCell(row, 2, c.FieldOffice);
                ReportTheme.SetCell(row, 3, ReportTheme.StatusLabel(c.Status.ToString()));
                ReportTheme.SetCell(row, 4, c.FiledOn.ToString("MMM d, yyyy"));

                // Past a year, flag it red — that is the number a supervisor is looking for.
                ReportTheme.SetCell(row, 5, days.ToString(), bold: days > 365,
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

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        _ => $"{bytes / 1024.0 / 1024.0:F1} MB"
    };
}
