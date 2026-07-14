using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;
using Naturalization.Api.Dtos;

namespace Naturalization.Api.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocuments(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/documents").WithTags("Documents");

        g.MapGet("/", async (NaturalizationDbContext db, int? caseId) =>
        {
            var query = db.Documents.AsNoTracking().AsQueryable();
            if (caseId is not null) query = query.Where(d => d.CaseId == caseId);

            var items = await query
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => DocumentDto.From(d))
                .ToListAsync();

            return TypedResults.Ok(items);
        })
        .WithName("ListDocuments");

        g.MapGet("/{id:int}", async Task<Results<Ok<DocumentDto>, NotFound>> (NaturalizationDbContext db, int id) =>
        {
            var d = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return d is null ? TypedResults.NotFound() : TypedResults.Ok(DocumentDto.From(d));
        })
        .WithName("GetDocument");

        /*
         * Metadata-only registration. This records that a piece of evidence
         * exists and hashes its identity; it does NOT accept file bytes.
         *
         * That is deliberate: accepting uploads of immigration evidence means
         * virus scanning, content-type sniffing, encryption at rest, retention
         * policy, and access logging — none of which belong in a demo, and all
         * of which a real deployer must decide for themselves. See README.
         */
        g.MapPost("/", async Task<Results<Created<DocumentDto>, ValidationProblem>> (
            NaturalizationDbContext db, DocumentInput input) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (!await db.Cases.AnyAsync(c => c.Id == input.CaseId))
                errors["caseId"] = [$"No case with id {input.CaseId}."];

            if (string.IsNullOrWhiteSpace(input.DocumentType))
                errors["documentType"] = ["Document type is required."];

            if (string.IsNullOrWhiteSpace(input.FileName))
                errors["fileName"] = ["File name is required."];

            if (input.SizeBytes <= 0)
                errors["sizeBytes"] = ["Size must be greater than zero."];

            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            var sha = Convert.ToHexString(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                    $"{input.CaseId}:{input.DocumentType}:{input.FileName}:{input.SizeBytes}")))
                .ToLowerInvariant();

            var d = new EvidenceDocument
            {
                CaseId = input.CaseId,
                DocumentType = input.DocumentType,
                FileName = input.FileName,
                ContentType = string.IsNullOrWhiteSpace(input.ContentType) ? "application/pdf" : input.ContentType,
                SizeBytes = input.SizeBytes,
                Sha256 = sha,
                Status = DocumentStatus.Received,
                UploadedAt = DateTime.UtcNow
            };

            db.Documents.Add(d);
            db.Events.Add(new CaseEvent
            {
                CaseId = input.CaseId,
                EventType = "Evidence filed",
                OccurredAt = DateTime.UtcNow,
                Actor = "System",
                Notes = $"{input.DocumentType} ({input.FileName}) registered."
            });

            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/documents/{d.Id}", DocumentDto.From(d));
        })
        .WithName("CreateDocument");

        g.MapPut("/{id:int}/status", async Task<Results<Ok<DocumentDto>, NotFound, ValidationProblem>> (
            NaturalizationDbContext db, int id, string status) =>
        {
            var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return TypedResults.NotFound();

            if (!Enum.TryParse<DocumentStatus>(status, out var s))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [$"'{status}' is not a valid document status."]
                });
            }

            d.Status = s;
            await db.SaveChangesAsync();
            return TypedResults.Ok(DocumentDto.From(d));
        })
        .WithName("SetDocumentStatus");

        g.MapDelete("/{id:int}", async Task<Results<NoContent, NotFound>> (NaturalizationDbContext db, int id) =>
        {
            var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return TypedResults.NotFound();

            db.Documents.Remove(d);
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        })
        .WithName("DeleteDocument");
    }
}