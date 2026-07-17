using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Naturalization.Api.Domain;

namespace Naturalization.Api.Auth;

public class TokenIssuer(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _jwt = options.Value;

    public (string Token, DateTime ExpiresAt) Issue(ApplicationUser officer)
    {
        var expires = DateTime.UtcNow.AddMinutes(_jwt.LifetimeMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));

        /*
         * JsonWebTokenHandler, not JwtSecurityTokenHandler.
         *
         * The older handler applies the OUTBOUND claim map on the way out, so
         * "sub" is written into the token as a schemas.xmlsoap.org URI. The
         * bearer handler (with MapInboundClaims = false) then looks for a plain
         * "sub" and finds nothing. Two halves of the same footgun; this handler
         * has neither half.
         */
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = officer.Id.ToString(),
                ["email"] = officer.Email,
                ["name"] = officer.FullName,
                ["field_office"] = officer.FieldOffice,
                // The local bearer scheme's RoleClaimType is "role", so this is
                // what RequireRole (and ClaimsPrincipal.IsInRole) reads. See
                // Auth/Policies.cs and AuthExtensions.
                ["role"] = officer.Role.ToString(),
            },
        });

        return (token, expires);
    }
}
