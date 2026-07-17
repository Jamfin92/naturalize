using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Dtos;

namespace Naturalization.Api.Endpoints;

/// <summary>
/// The town and country lookups behind <see cref="Domain.Applicant.TownCode"/>
/// and <see cref="Domain.Applicant.CountryCode"/>. The SPA reads these to turn a
/// stored code into a human label, and to populate the pickers on the form.
/// </summary>
public static class LookupEndpoints
{
    public static void MapLookups(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/lookups")
            .WithTags("Lookups")
            .RequireAuthorization();

        g.MapGet("/towns", async (NaturalizationDbContext db) =>
        {
            var towns = await db.TownCodes.AsNoTracking()
                .OrderBy(t => t.Description)
                .Select(t => LookupDto.From(t))
                .ToListAsync();
            return TypedResults.Ok(towns);
        })
        .WithName("ListTownCodes")
        .WithSummary("Every town code, ordered by description.");

        g.MapGet("/countries", async (NaturalizationDbContext db) =>
        {
            var countries = await db.CountryCodes.AsNoTracking()
                .OrderBy(c => c.Description)
                .Select(c => LookupDto.From(c))
                .ToListAsync();
            return TypedResults.Ok(countries);
        })
        .WithName("ListCountryCodes")
        .WithSummary("Every country code, ordered by description.");
    }
}
