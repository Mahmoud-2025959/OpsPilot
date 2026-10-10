using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// Join entity between a <see cref="Team"/> and a <see cref="User"/>.
/// </summary>
/// <remarks>
/// Intentionally not a <see cref="BaseEntity"/>: it is identified by the composite
/// <c>(TeamId, UserId)</c> key, carries no audit history, and is never soft-deleted —
/// membership is simply removed.
/// </remarks>
public sealed class TeamMember : ITenantOwned
{
    private TeamMember(Guid teamId, Guid userId, Guid tenantId, TeamRole role, DateTimeOffset joinedAt)
    {
        TeamId = teamId;
        UserId = userId;
        TenantId = tenantId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid TeamId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public TeamRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public static TeamMember Create(
        Guid teamId,
        Guid userId,
        Guid tenantId,
        TeamRole role,
        DateTimeOffset? joinedAt = null)
    {
        return new TeamMember(teamId, userId, tenantId, role, joinedAt ?? DateTimeOffset.UtcNow);
    }

    public void ChangeRole(TeamRole role)
    {
        Role = role;
    }
}
