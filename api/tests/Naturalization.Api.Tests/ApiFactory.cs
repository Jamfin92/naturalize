using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Naturalization.Api.Data;

namespace Naturalization.Api.Tests;

/// <summary>
/// Boots the real API — real pipeline, real auth, real migrations — against a
/// throwaway SQLite file.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"naturalize-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        /*
         * Only Seed:Demo is overridden here, and deliberately NOT the JWT config.
         *
         * The same before/after-Build split that stops us setting the connection
         * string through configuration (see below) would actively BREAK auth if we
         * set the signing key here. Program.cs reads Auth:Jwt to build the token
         * VALIDATOR before builder.Build(), so it never sees this override and
         * falls back to appsettings.Development.json's key. But TokenIssuer reads
         * the key via IOptions AFTER Build, where the override DOES win. Result:
         * tokens signed with one key, validated with another — every request 401s
         * with a valid-looking token. So auth runs on the real appsettings key, and
         * only Seed:Demo (read post-Build, via app.Configuration) is switched.
         */
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                // Tests build their own fixtures; the 40-applicant demo caseload
                // would just be 40 rows of noise to filter out of every assertion.
                ["Seed:Demo"] = "false",
            }));

        builder.ConfigureServices(services =>
        {
            /*
             * Remove the existing registration before adding ours.
             *
             * AddDbContext registers via TryAdd, so calling it a second time is a
             * SILENT NO-OP — the app would keep its original SQLite file, the
             * tests would run against the developer's real naturalization.db, and
             * they would pass for entirely the wrong reason while quietly
             * scribbling on it.
             */
            Remove<DbContextOptions<NaturalizationDbContext>>(services);
            Remove<DbContextOptions>(services);
            Remove<NaturalizationDbContext>(services);

            services.AddDbContext<NaturalizationDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        });

        /*
         * Note this cannot be done through configuration. Program.cs reads the
         * connection string BEFORE builder.Build(), and WebApplicationFactory's
         * callbacks are replayed during Build() — so ConnectionStrings:Default set
         * via ConfigureAppConfiguration arrives too late to be seen. Replacing the
         * service registration is what actually works.
         *
         * A file, not :memory:. An in-memory SQLite database dies with the
         * connection that opened it, so it has to be kept alive by hand and shared
         * across every scoped DbContext — and then concurrent requests collide with
         * intermittent "database is locked". A temp file costs microseconds and
         * removes the whole failure class.
         */
    }

    private static void Remove<T>(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
        {
            services.Remove(descriptor);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        // Pooled connections keep a handle on the file, so it cannot be deleted
        // until they are actually closed.
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _dbPath, $"{_dbPath}-wal", $"{_dbPath}-shm" })
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }
}
