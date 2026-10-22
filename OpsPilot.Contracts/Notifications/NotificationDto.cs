namespace OpsPilot.Contracts.Notifications;

/// <summary>
/// One notification queued for a user (plan, FR-16).
/// </summary>
/// <remarks>
/// <see cref="Status"/> explains what has happened so far rather than what the user asked for:
/// an in-app row is sent the moment it is written, while an email row waits for a mailer and
/// therefore reads pending.
/// </remarks>
public sealed record NotificationDto(
    Guid Id,
    Guid UserId,
    Guid? IncidentId,
    string Channel,
    string Type,
    string Title,
    string Body,
    string Status,
    DateTimeOffset? SentAt,
    DateTimeOffset? ReadAt,
    bool IsRead,
    DateTimeOffset CreatedAt);
