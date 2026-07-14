using Naturalization.Api.Reports;
using Naturalization.Api.Services;

namespace Naturalization.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReports(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reports").WithTags("Reports");

        /*
         * The `.pdf` suffix in the route is deliberate. It means the browser can
         * be pointed straight at the URL with a plain <a href> — the extension,
         * the content type and Content-Disposition all agree, so the file lands
         * in Downloads with a sensible name and no JS blob juggling.
         */

        g.MapGet("/case/{caseId:int}.pdf", async Task<IResult> (
            IReportGenerator reports, int caseId, CancellationToken ct) =>
        {
            try
            {
                var pdf = await reports.CaseRecordAsync(caseId, ct);
                return Results.File(pdf, "application/pdf", $"case-record-{caseId}.pdf");
            }
            catch (KeyNotFoundException e)
            {
                return Results.Problem(e.Message, statusCode: StatusCodes.Status404NotFound);
            }
        })
        .WithName("CaseRecordPdf")
        .WithSummary("The complete file for one case: particulars, decision, evidence, audit trail.")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
        .Produces(StatusCodes.Status404NotFound);

        g.MapGet("/approvals.pdf", async Task<IResult> (
            IReportGenerator reports,
            DateOnly? from,
            DateOnly? to,
            string? fieldOffice,
            CancellationToken ct) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Default to the year to date — the range a supervisor asks for most.
            var start = from ?? new DateOnly(today.Year, 1, 1);
            var end = to ?? today;

            if (start > end)
                return Results.Problem("'from' must not be after 'to'.", statusCode: 400);

            var pdf = await reports.ApprovalsAsync(start, end, fieldOffice, ct);
            return Results.File(pdf, "application/pdf", $"approvals-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf");
        })
        .WithName("ApprovalsPdf")
        .WithSummary("Decisions in a date range, with approve/deny counts by field office.")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        g.MapGet("/pipeline.pdf", async (IReportGenerator reports, CancellationToken ct) =>
        {
            var pdf = await reports.PipelineAsync(ct);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return Results.File(pdf, "application/pdf", $"pipeline-{today:yyyyMMdd}.pdf");
        })
        .WithName("PipelinePdf")
        .WithSummary("Current caseload by status, with aging and the oldest pending matters.")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        // Dashboard metrics. Not a report, but it shares CaseMetrics with the
        // pipeline PDF so the screen and the print-out cannot disagree.
        app.MapGet("/api/metrics", async (CaseMetrics metrics) => TypedResults.Ok(await metrics.ComputeAsync()))
            .WithTags("Reports")
            .WithName("GetMetrics");
    }
}
