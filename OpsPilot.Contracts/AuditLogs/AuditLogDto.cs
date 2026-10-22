namespace OpsPilot.Contracts.AuditLogs;

/// <summary>
/// One entry of the tenant's audit trail (plan, FR-18).
/// </summary>
/// <remarks>
/// <see cref="OldValues"/> and <see cref="NewValues"/> are the raw JSON snapshots recorded at
/// the time of the action rather than a reshaped projection: what was stored is exactly what the
/// trail shows, so a later change to how a value is presented cannot rewrite history.
/// </remarks>
public sealed record AuditLogDto(
    Guid Id,
    string Action,
    string EntityType,
    string EntityId,
    Guid? ActorUserId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    DateTimeOffset CreatedAt);
