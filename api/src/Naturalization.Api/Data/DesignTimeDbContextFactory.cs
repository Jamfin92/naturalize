using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Naturalization.Api.Data;

/// <summary>
/// Lets `dotnet ef` build a DbContext without running the application.
///
/// Without this, EF's design-time services resolve the context by EXECUTING the
/// entry point up to builder.Build(). That works until it doesn't: the tooling
/// runs with no ASPNETCORE_ENVIRONMENT, i.e. as Production, so anything in
/// Program.cs that reads deployment config or validates a secret will fire during
/// `migrations add` and fail with the famously unhelpful "Unable to create an
/// object of type 'NaturalizationDbContext'".
///
/// Migrations only need the model, and the model only needs a provider. So give
/// the tooling exactly that and nothing else.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NaturalizationDbContext>
{
    public NaturalizationDbContext CreateDbContext(string[] args)
    {
        // The design-time connection only needs to reach *a* SQL Server instance
        // so EF can read provider metadata and scaffold SQL Server-shaped
        // migrations; it never touches production data. Override it with
        // ConnectionStrings__Default in the environment if the local default here
        // is not where your instance lives.
        var connection =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost,1433;Database=naturalize;User Id=sa;Password=Change-me-123;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<NaturalizationDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new NaturalizationDbContext(options);
    }
}
