namespace Naturalization.Api.Auth;

/// <summary>Bound from configuration section <c>Auth:Jwt</c>.</summary>
public class JwtOptions
{
    public const string Section = "Auth:Jwt";

    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";

    /// <summary>
    /// HS256 signing key. Empty in appsettings.json by design — a real key is
    /// supplied per-deployment via <c>Auth__Jwt__Key</c> or user-secrets, and
    /// never committed. Validated at startup: HMAC-SHA256 requires at least 256
    /// bits, and a short key does not fail at boot, it fails on the first login
    /// with an opaque IDX10653.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>Eight hours — one shift. There are no refresh tokens.</summary>
    public int LifetimeMinutes { get; set; } = 480;
}

/// <summary>
/// Bound from <c>Auth:Okta</c>. Inert until <see cref="Enabled"/> is true.
///
/// The carve-out is real, not decorative: set Authority + Audience, flip Enabled,
/// and the API validates Okta-issued tokens alongside locally-issued ones — no
/// code change. See AuthExtensions for the scheme selector that routes between
/// them. The FRONTEND still needs an OIDC redirect flow; that part is not built.
/// </summary>
public class OktaOptions
{
    public const string Section = "Auth:Okta";

    public bool Enabled { get; set; }

    /// <summary>e.g. "https://dev-123456.okta.com/oauth2/default"</summary>
    public string Authority { get; set; } = "";

    /// <summary>e.g. "api://naturalize"</summary>
    public string Audience { get; set; } = "";
}
