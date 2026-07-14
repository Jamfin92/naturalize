using System.Security.Claims;

namespace Naturalization.Api.Auth;

/// <summary>
/// The only sanctioned way to learn who is making a request.
///
/// Before auth existed, the audit trail's actor arrived in the REQUEST BODY:
/// endpoints took an `actor` / `decidedBy` string and defaulted it to "Unknown
/// officer". The caller asserted their own identity, which means the trail could
/// be signed with anyone's name. These read the bearer token instead, and the
/// body fields are gone.
/// </summary>
public static class OfficerPrincipal
{
    public const string UnknownActor = "Unknown officer";

    public static int? OfficerId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;

    /// <summary>
    /// The display name recorded against every audit row.
    ///
    /// This reads the raw "sub"/"name" claims, which only works because both JWT
    /// schemes set MapInboundClaims = false. Leave that default on and .NET
    /// rewrites "name" to a schemas.xmlsoap.org URI, this returns null, and every
    /// audit row silently records "Unknown officer" — the exact failure the whole
    /// feature exists to prevent.
    /// </summary>
    public static string OfficerName(this ClaimsPrincipal user) =>
        user.FindFirstValue("name") ?? user.FindFirstValue("email") ?? UnknownActor;

    public static string OfficerEmail(this ClaimsPrincipal user) =>
        user.FindFirstValue("email") ?? "";

    public static string OfficerFieldOffice(this ClaimsPrincipal user) =>
        user.FindFirstValue("field_office") ?? "";
}
