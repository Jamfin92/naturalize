using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OpenApi.Models;
using Naturalization.Api.Auth;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;
using Naturalization.Api.Endpoints;
using Naturalization.Api.Reports;
using Naturalization.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Persistence -------------------------------------------------------------
// SQLite: the whole database is one file, so cloning the repo and running
// `dotnet run` gives you a working system with no container and no server.
var connection = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=naturalization.db";

builder.Services.AddDbContext<NaturalizationDbContext>(o => o
    .UseSqlite(connection)
    /*
     * EF warns that Case, Decision, EvidenceDocument and CaseEvent each have a
     * REQUIRED navigation to a principal that carries a query filter, because the
     * naive version of that model hands you a dependent whose required parent has
     * been filtered away — i.e. a null reference where the type system promised
     * you an object.
     *
     * Suppressed because the model deliberately avoids exactly that: every
     * dependent's filter INCLUDES its principal's (see OnModelCreating), so a
     * dependent can never come back without its parent. Suppressed knowingly,
     * not swept under the rug.
     */
    .ConfigureWarnings(w => w.Ignore(
        CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

builder.Services.AddScoped<CaseMetrics>();
builder.Services.AddScoped<IReportGenerator, MigraDocReportGenerator>();

// --- Authentication ----------------------------------------------------------
// Local JWT forms auth, plus a dormant Okta scheme. See Auth/AuthExtensions.cs.
builder.Services.AddNaturalizeAuth(builder.Configuration);

// --- CORS --------------------------------------------------------------------
// Open to the Vite dev server only. A real deployment must replace this with
// its own origins.
const string DevCors = "dev";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    // Cross-origin JS cannot read a response header unless it is explicitly
    // exposed — including Content-Disposition. Without this the report downloads
    // still work but arrive named "download", because the filename the API
    // carefully set is invisible to the code that saves the blob.
    .WithExposedHeaders("Content-Disposition")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new()
    {
        Title = "Naturalize API",
        Version = "v1",
        Description = "Open-source naturalization records system. Demo data; not affiliated with USCIS."
    });

    // Swashbuckle does not infer security from RequireAuthorization() on minimal
    // APIs, so without this the "Authorize" button simply isn't there and every
    // protected endpoint 401s from Swagger UI with no way to fix it.
    //
    // Type Http + scheme "bearer" (rather than ApiKey) so Swagger UI prepends
    // "Bearer " itself; with ApiKey the user has to type the word, and forgetting
    // to is the single most common "my token doesn't work" report.
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the accessToken from POST /api/auth/login. Do not include the word 'Bearer'."
    });

    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            // Must string-match the definition name above, or Swagger UI silently
            // sends no header at all.
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

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

    /*
     * Migrate, not EnsureCreated. EnsureCreated builds the schema from the model
     * and then can never change it again — the first time you add a column, your
     * only options are "drop the database" or "hand-write SQL", and neither is
     * available to someone holding real records.
     *
     * NOTE for anyone upgrading a pre-migrations checkout: an EnsureCreated
     * database has no __EFMigrationsHistory table, so Migrate() will believe
     * nothing has been applied and try to CREATE TABLE over the top of your
     * existing tables. Delete naturalization.db (and its -wal / -shm siblings)
     * once, and this rebuilds it.
     */
    await db.Database.MigrateAsync();

    // Officers are infrastructure, not fixtures: a database with no officer in it
    // is an API that nobody can log into. Always seeded.
    await DbInitializer.SeedOfficersAsync(
        db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<OfficerAccount>>());

    // The 40-applicant demo caseload IS a fixture. Read from app.Configuration
    // (i.e. after Build()) so the integration-test host can switch it off and get
    // a clean database.
    if (app.Configuration.GetValue("Seed:Demo", true))
    {
        await DbInitializer.SeedDemoCaseloadAsync(db);
    }
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

/*
 * Order matters here, and getting it wrong produces a bug that looks like
 * anything but an auth bug.
 *
 * Calling AddAuthentication() makes ASP.NET auto-insert UseAuthentication and
 * UseAuthorization at the FRONT of the pipeline — ahead of UseCors. A
 * cross-origin preflight is an OPTIONS request with no Authorization header, so
 * it would hit the authorization middleware, get a 401, and never reach the CORS
 * middleware that was supposed to answer it. The browser then reports a generic
 * CORS failure with no hint that authentication is involved, on every mutation
 * the SPA attempts.
 *
 * Calling all three explicitly, in this order, suppresses the auto-added pair.
 */
app.UseCors(DevCors);
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }))
    .WithTags("Health")
    .WithName("Health")
    .AllowAnonymous();

app.MapAuth();
app.MapApplicants();
app.MapCases();
app.MapDecisions();
app.MapDocuments();
app.MapReports();

app.Run();

/// <summary>
/// Top-level statements compile to an INTERNAL Program class, which
/// WebApplicationFactory&lt;Program&gt; cannot see from the test assembly. This
/// marker makes it public. Without it the integration tests do not compile.
/// </summary>
public partial class Program;
