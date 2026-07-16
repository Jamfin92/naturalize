using Microsoft.AspNetCore.Authorization;
using Naturalization.Api.Domain;

namespace Naturalization.Api.Auth;

/// <summary>
/// The authorisation policies that gate applicant changes by officer role.
///
/// The role travels in the "role" claim of the bearer token (see TokenIssuer),
/// and both JWT schemes set RoleClaimType so <c>RequireRole</c> reads it — "role"
/// for the local scheme, "groups" for Okta. Read endpoints stay open to any
/// authenticated officer; only the mutating ones require a policy.
/// </summary>
public static class Policies
{
    /// <summary>Create or update an applicant record. Officer or Admin.</summary>
    public const string ManageApplicants = "ManageApplicants";

    /// <summary>Withdraw or restore an applicant record. Admin only.</summary>
    public const string WithdrawApplicants = "WithdrawApplicants";

    public static AuthorizationBuilder AddNaturalizePolicies(this AuthorizationBuilder builder) =>
        builder
            .AddPolicy(ManageApplicants, p => p.RequireRole(
                nameof(OfficerRole.Officer), nameof(OfficerRole.Admin)))
            .AddPolicy(WithdrawApplicants, p => p.RequireRole(
                nameof(OfficerRole.Admin)));
}
