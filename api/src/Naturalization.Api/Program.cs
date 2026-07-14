using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Endpoints;
using Naturalization.Api.Reports;
using Naturalization.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Persistence -------------------------------------------------------------
// SQLite: the whole database is one file, so cloning the repo and running
// `dotnet run` gives you a working system with no container and no server.
var connection = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=naturalization.db";

builder.Services.AddDbContext<NaturalizationDbContext>(o => o.UseSqlite(connection));

builder.Services.AddScoped<CaseMetrics>();
builder.Services.AddScoped<IReportGenerator, MigraDocReportGenerator>();

// --- CORS --------------------------------------------------------------------
// Open to the Vite dev server only. A real deployment must replace this with
// its own origins.
const string DevCors = "dev";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new()
{
    Title = "Naturalize API",
    Version = "v1",
    Description = "Open-source naturalization records system. Demo data; not affiliated with USCIS."
}));

builder.Services.AddProblemDetails();

var app = builder.Build();

/*
 * Install the PDF font resolver once, at startup, before any request can trigger
 * a render. PdfSharp's GlobalFontSettings.FontResolver is a static singleton
 * that throws if assigned twice, and it has no default on macOS or Linux — so
 * installing it lazily on first render is how you get an intermittent 500 that
 * only reproduces under concurrency.
 */
EmbeddedFontResolver.EnsureInstalled();

// --- Database ----------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NaturalizationDbContext>();

    // EnsureCreated, not Migrate: the schema ships as a throwaway demo dataset.
    // A deployment holding real records must switch to `dotnet ef migrations`.
    await db.Database.EnsureCreatedAsync();
    await DbInitializer.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "Naturalize API v1");
        o.DocumentTitle = "Naturalize API";
    });
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(DevCors);

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }))
    .WithTags("Health")
    .WithName("Health");

app.MapApplicants();
app.MapCases();
app.MapDecisions();
app.MapDocuments();
app.MapReports();

app.Run();
