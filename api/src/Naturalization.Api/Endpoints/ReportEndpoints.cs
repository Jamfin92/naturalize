using Naturalization.Api.Reports;

namespace Naturalization.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReports(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        /*
         * These are behind auth, which changed how the client fetches them.
         *
         * They used to be plain <a href> links: the browser navigated to the URL
         * and Content-Disposition did the rest. But a browser NAVIGATION cannot
         * carry an Authorization header, so the moment this group required a token
         * every download button would have opened a blank tab containing 401 JSON.
         * The SPA now fetches them with the bearer token and saves the blob (see
         * src/lib/api.ts). CORS must expose Content-Disposition for the filename
         * below to survive the trip — see Program.cs.
         */

        g.MapGet("/approvals.pdf", async Task<IResult> (
            IReportGenerator reports,
            DateOnly? from,
            DateOnly? to,
            CancellationToken ct) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Default to the year to date — the range a supervisor asks for most.
            var start = from ?? new DateOnly(today.Year, 1, 1);
            var end = to ?? today;

            if (start > end)
                return Results.Problem("'from' must not be after 'to'.", statusCode: 400);

            var pdf = await reports.ApprovalsAsync(start, end, ct);
            return Results.File(pdf, "application/pdf", $"approvals-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf");
        })
        .WithName("ApprovalsPdf")
        .WithSummary("Decisions in a date range, with approve/deny counts.")
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

        g.MapGet("/labels.pdf", async Task<IResult> (
            IReportGenerator reports,
            DateOnly? from,
            DateOnly? to,
            CancellationToken ct) =>
        {
            // Both bounds optional: a single date is from == to, an open-ended range
            // sets one, and neither prints every active applicant.
            if (from is DateOnly f && to is DateOnly t && f > t)
                return Results.Problem("'from' must not be after 'to'.", statusCode: 400);

            var pdf = await reports.MailingLabelsAsync(from, to, ct);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var name = (from, to) switch
            {
                (DateOnly a, DateOnly b) when a == b => $"mailing-labels-{a:yyyyMMdd}.pdf",
                (DateOnly a, DateOnly b) => $"mailing-labels-{a:yyyyMMdd}-{b:yyyyMMdd}.pdf",
                (DateOnly a, null) => $"mailing-labels-from-{a:yyyyMMdd}.pdf",
                (null, DateOnly b) => $"mailing-labels-to-{b:yyyyMMdd}.pdf",
                _ => $"mailing-labels-{today:yyyyMMdd}.pdf",
            };
            return Results.File(pdf, "application/pdf", name);
        })
        .WithName("MailingLabelsPdf")
        .WithSummary("Avery 5160 mailing labels — one per active applicant, optionally by date added (single date or range).")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf");
    }
}
