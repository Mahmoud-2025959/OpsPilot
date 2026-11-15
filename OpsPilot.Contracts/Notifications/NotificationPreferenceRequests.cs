namespace OpsPilot.Contracts.Notifications;

/// <summary>One severity-over-channel choice in a preferences save (plan, UC-13).</summary>
public sealed record NotificationPreferenceItem(
    string? Severity,
    string? Channel,
    bool Enabled);

/// <summary>
/// Saves the caller's notification preferences. The list is partial — combinations that are
/// left out keep whatever state they were in — and a repeated severity-and-channel pair is
/// refused rather than resolved by last-write-wins.
/// </summary>
public sealed record UpdateNotificationPreferencesRequest(
    IReadOnlyList<NotificationPreferenceItem>? Preferences);
