using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Auth;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;
using Naturalization.Api.Dtos;

namespace Naturalization.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        g.MapPost("/login", async Task<Results<Ok<LoginResult>, UnauthorizedHttpResult>> (
            NaturalizationDbContext db,
            IPasswordHasher<OfficerAccount> hasher,
            TokenIssuer tokens,
            LoginInput input) =>
        {
            var email = (input.Email ?? "").Trim();
            var officer = await db.Officers.FirstOrDefaultAsync(o => o.Email == email);

            /*
             * One 401 for every failure — unknown account, wrong password,
             * deactivated account. Distinguishing them turns this endpoint into an
             * oracle that tells an attacker which addresses are real officers.
             *
             * The verification still runs against a DUMMY hash when the account is
             * missing, so a "no such user" answer doesn't come back measurably
             * faster than a "wrong password" one.
             */
            var hash = officer?.PasswordHash ?? DummyHash;
            var result = hasher.VerifyHashedPassword(officer ?? new OfficerAccount(), hash, input.Password ?? "");

            // SuccessRehashNeeded is a SUCCESS (the hash just used older
            // parameters). Comparing `== Success` alone locks people out on the
            // day the hasher's defaults change.
            var ok = result is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;

            if (officer is null || !officer.IsActive || !ok) return TypedResults.Unauthorized();

            var (token, expiresAt) = tokens.Issue(officer);
            return TypedResults.Ok(new LoginResult(token, expiresAt, OfficerDto.From(officer)));
        })
        .AllowAnonymous()
        .WithName("Login")
        .WithSummary("Exchange officer credentials for a bearer token.");

        /*
         * The SPA calls this on boot to find out whether the token in localStorage
         * is still good. Without it, an expired token would let you walk straight
         * into the shell and then 401 on every panel.
         */
        g.MapGet("/me", async Task<Results<Ok<OfficerDto>, UnauthorizedHttpResult>> (
            NaturalizationDbContext db, ClaimsPrincipal user) =>
        {
            var id = user.OfficerId();
            if (id is null) return TypedResults.Unauthorized();

            var officer = await db.Officers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
            if (officer is null || !officer.IsActive) return TypedResults.Unauthorized();

            return TypedResults.Ok(OfficerDto.From(officer));
        })
        .RequireAuthorization()
        .WithName("Me")
        .WithSummary("The signed-in officer, resolved from the bearer token.");
    }

    /// <summary>
    /// A real PBKDF2 hash of a value nobody knows, used to keep the timing of a
    /// failed login roughly constant whether or not the account exists.
    /// </summary>
    private static readonly string DummyHash = new PasswordHasher<OfficerAccount>()
        .HashPassword(new OfficerAccount(), Guid.NewGuid().ToString());
}
