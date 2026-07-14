using Naturalization.Api.Reports;
using Naturalization.Api.Services;

namespace Naturalization.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReports(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        /*
         * These are now behind auth, which changed how the client fetches them.
         *
         * They used to be plain <a href> links: the browser navigated to the URL
         * and Content-Disposition did the rest. But a browser NAVIGATION cannot
         * carry an Authorization header, so the moment this group required a token
         * every download button would have opened a blank tab containing 401 JSON.
         * The SPA now fetches them with the bearer token and saves the blob (see
         * src/lib/api.ts). CORS must expose Content-Disposition for the filename
         * below to survive the trip — see Program.cs.
         *
         * The alternative — accepting ?access_token= on these routes — would have
         * kept the <a href> and leaked a full-lifetime bearer token into browser
         * history, the Referer header, and every proxy log in between.
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

        // The /api/metrics dashboard endpoint moved to the enhancement branch with
        // the dashboard screen that consumed it. CaseMetrics itself stays — the
        // pipeline PDF above still depends on it.
    }
}
