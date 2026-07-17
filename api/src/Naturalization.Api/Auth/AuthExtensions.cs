using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Naturalization.Api.Domain;

namespace Naturalization.Api.Auth;

public static class AuthExtensions
{
    public const string LocalScheme = "Local";
    public const string OktaScheme = "Okta";

    /// <summary>The default scheme: a policy scheme that routes to Local or Okta.</summary>
    public const string SmartScheme = "Smart";

    public static IServiceCollection AddNaturalizeAuth(
        this IServiceCollection services,
        IConfiguration config)
    {
        /*
         * Validate the signing key in EVERY environment, and do it with
         * ValidateOnStart rather than a bare `throw` up in Program.cs.
         *
         * A bare throw is wrong twice over. `dotnet ef` runs the entry point up to
         * builder.Build() with ASPNETCORE_ENVIRONMENT unset — i.e. as Production —
         * so an "if (!IsDevelopment()) throw" fires during `migrations add` and
         * you get the useless "Unable to create an object of type
         * 'NaturalizationDbContext'". Meanwhile in Development it does NOT fire,
         * so a too-short key sails through boot and 500s on the first login
         * instead. ValidateOnStart runs when the HOST starts: never at design
         * time, always at run time.
         */
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.Section))
            .Validate(
                o => Encoding.UTF8.GetByteCount(o.Key) >= 32,
                "Auth:Jwt:Key must be at least 32 bytes (256 bits) for HS256. "
                    + "Generate one with `openssl rand -base64 48` and supply it via "
                    + "the Auth__Jwt__Key environment variable.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Auth:Jwt:Issuer and Auth:Jwt:Audience are both required.")
            .ValidateOnStart();

        services.AddOptions<OktaOptions>().Bind(config.GetSection(OktaOptions.Section));

        // PBKDF2. Registering just the hasher gets us password verification
        // without dragging in the whole of ASP.NET Identity (stores, roles, UI).
        services.AddSingleton<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
        services.AddScoped<TokenIssuer>();

        var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        var okta = config.GetSection(OktaOptions.Section).Get<OktaOptions>() ?? new OktaOptions();
        var oktaIssuer = okta.Authority.TrimEnd('/');

        var authn = services.AddAuthentication(SmartScheme);

        /*
         * The Okta carve-out, in one function.
         *
         * A policy scheme is a scheme that doesn't authenticate anything itself —
         * it peeks at the request and forwards to a scheme that does. So a token
         * from Okta gets validated against Okta's signing keys, one from us gets
         * validated against ours, and neither knows about the other.
         *
         * Three ways to get this wrong, all of which are 500s rather than 401s:
         *
         *   - Naming a scheme that isn't registered. If Okta is disabled its
         *     handler does not exist, and forwarding to it throws
         *     "No authentication handler is registered for the scheme 'Okta'" on
         *     every request. Hence the early return before we even look at the token.
         *   - Returning null (yields a 401 with no WWW-Authenticate) or returning
         *     SmartScheme itself (forwards to itself, forever).
         *   - Letting a malformed token escape. Parsing garbage throws, and an
         *     unhandled throw here is a 500 — so a bad token would crash the API
         *     rather than being rejected. Anything unparseable goes to Local, which
         *     produces a clean 401.
         */
        authn.AddPolicyScheme(SmartScheme, "Local JWT or Okta", o =>
        {
            o.ForwardDefaultSelector = ctx =>
            {
                if (!okta.Enabled) return LocalScheme;

                if (!AuthenticationHeaderValue.TryParse(ctx.Request.Headers.Authorization, out var header)
                    || !"Bearer".Equals(header.Scheme, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(header.Parameter))
                {
                    // No token at all: hand to Local so IT issues the challenge.
                    return LocalScheme;
                }

                try
                {
                    // Peek at the issuer. Never validate here — that is the
                    // handler's job, and doing it twice is how you get a scheme
                    // that trusts a token it hasn't checked.
                    var token = new JsonWebToken(header.Parameter);
                    return token.TryGetPayloadValue<string>("iss", out var issuer)
                        && !string.IsNullOrEmpty(oktaIssuer)
                        && issuer.TrimEnd('/').Equals(oktaIssuer, StringComparison.OrdinalIgnoreCase)
                            ? OktaScheme
                            : LocalScheme;
                }
                catch (ArgumentException)
                {
                    // SecurityTokenMalformedException derives from ArgumentException.
                    return LocalScheme;
                }
            };
        });

        authn.AddJwtBearer(LocalScheme, o =>
        {
            // See OfficerPrincipal: leave this true (the default!) and .NET renames
            // "sub"/"name"/"email" to schemas.xmlsoap.org URIs behind your back,
            // and the audit trail quietly records "Unknown officer" forever.
            o.MapInboundClaims = false;

            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ValidateLifetime = true,

                // Defaults to five minutes, which would make the `expiresAt` we
                // hand the client a lie by up to five minutes.
                ClockSkew = TimeSpan.Zero,

                NameClaimType = "name",
                RoleClaimType = "role",
            };
        });

        // Registering this with a blank Authority throws at STARTUP ("The
        // MetadataAddress or Authority must use HTTPS"), so the guard is
        // load-bearing rather than stylistic.
        if (okta.Enabled)
        {
            authn.AddJwtBearer(OktaScheme, o =>
            {
                o.Authority = okta.Authority;
                o.Audience = okta.Audience;
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "groups",
                    ClockSkew = TimeSpan.Zero,
                };
            });
        }

        // Role-gated policies for the applicant mutations. Read endpoints keep the
        // bare authenticated requirement; see Auth/Policies.cs.
        services.AddAuthorizationBuilder().AddNaturalizePolicies();
        return services;
    }
}
