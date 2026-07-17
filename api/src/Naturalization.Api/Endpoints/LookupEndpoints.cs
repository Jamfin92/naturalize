using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Data;
using Naturalization.Api.Dtos;

namespace Naturalization.Api.Endpoints;

/// <summary>
/// The lookups the SPA reads to populate the pickers on the applicant form: the
/// residential localities behind <see cref="Domain.Applicant.LocalityId"/>, and
/// the country-of-birth codes behind <see cref="Domain.Applicant.CountryCode"/>.
/// </summary>
public static class LookupEndpoints
{
    public static void MapLookups(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/lookups")
            .WithTags("Lookups")
            .RequireAuthorization();

        g.MapGet("/localities", async (NaturalizationDbContext db) =>
        {
            var localities = await db.Localities.AsNoTracking()
                .OrderBy(l => l.Name).ThenBy(l => l.State)
                .Select(l => LocalityDto.From(l))
                .ToListAsync();
            return TypedResults.Ok(localities);
        })
        .WithName("ListLocalities")
        .WithSummary("Every residential locality (ZIP, city/town, state), ordered by name.");

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
