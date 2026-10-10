using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// An immutable record of a state-changing action (ERD §1, "إضافة فقط").
/// </summary>
/// <remarks>
/// Implements <see cref="IAppendOnly"/> so the trail can never be edited or deleted.
/// <see cref="ActorUserId"/> is deliberately <em>not</em> a foreign key (ERD §4): the
/// trail has to stay intact even after the acting user is disabled or removed.
/// </remarks>
public sealed class AuditLog : TenantOwnedEntity, IAppendOnly
{
    private AuditLog(
        Guid tenantId,
        Guid? actorUserId,
        string action,
        string entityType,
        string entityId,
        string? oldValues,
        string? newValues,
        string? ipAddress,
        DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OldValues = oldValues;
        NewValues = newValues;
        IpAddress = ipAddress;
        CreatedAt = createdAt;
    }

    /// <summary>User that performed the action; null for system actions such as SLA sweeps.</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>What happened, e.g. <c>RoleChanged</c>.</summary>
    public string Action { get; private set; }

    public string EntityType { get; private set; }

    public string EntityId { get; private set; }

    /// <summary>JSON snapshot of the entity before the change.</summary>
    public string? OldValues { get; private set; }

    /// <summary>JSON snapshot of the entity after the change.</summary>
    public string? NewValues { get; private set; }

    public string? IpAddress { get; private set; }

    public static AuditLog Create(
        Guid tenantId,
        string action,
        string entityType,
        string entityId,
        Guid? actorUserId = null,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        return new AuditLog(
            tenantId,
            actorUserId,
            action.Trim(),
            entityType.Trim(),
            entityId.Trim(),
            oldValues,
            newValues,
            ipAddress?.Trim(),
            createdAt ?? DateTimeOffset.UtcNow);
    }
}
