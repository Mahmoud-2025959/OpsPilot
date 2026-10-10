using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// A user's opt-in/opt-out choice for one severity over one channel.
/// </summary>
/// <remarks>
/// Uniqueness on <c>(UserId, Severity, Channel)</c> means a user can never accumulate
/// duplicate rows for the same combination.
/// </remarks>
public sealed class NotificationPreference : TenantOwnedEntity
{
    private NotificationPreference(Guid userId, NotificationChannel channel, Severity severity)
    {
        UserId = userId;
        Channel = channel;
        Severity = severity;
    }

    public Guid UserId { get; private set; }

    public NotificationChannel Channel { get; private set; }

    /// <summary>Severity band this preference applies to (ERD's <c>Severity</c>).</summary>
    public Severity Severity { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public static NotificationPreference Create(
        Guid tenantId,
        Guid userId,
        NotificationChannel channel,
        Severity severity)
    {
        return new NotificationPreference(userId, channel, severity)
        {
            TenantId = tenantId
        };
    }

    public void Enable()
    {
        IsEnabled = true;
    }

    public void Disable()
    {
        IsEnabled = false;
    }
}
