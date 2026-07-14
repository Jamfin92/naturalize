using System.Security.Claims;
using Naturalization.Api.Auth;
using Naturalization.Api.Data;
using Naturalization.Api.Domain;

namespace Naturalization.Api.Services;

/// <summary>
/// Writes the system-level audit trail. Adds the row to the change tracker but
/// does NOT save — the caller commits it in the same transaction as the change it
/// describes, so an audit row can never exist for a change that got rolled back,
/// and a change can never commit without its audit row.
/// </summary>
public static class AuditLog
{
    public static void Record(
        NaturalizationDbContext db,
        ClaimsPrincipal user,
        string entityType,
        int entityId,
        string action,
        string summary)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Actor = user.OfficerName(),
            OccurredAt = DateTime.UtcNow,
            Summary = summary,
        });
    }
}
