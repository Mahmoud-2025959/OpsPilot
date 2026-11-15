namespace OpsPilot.Contracts.Notifications;

/// <summary>
/// The effective state of one severity-over-channel pair for a user (plan, FR-16).
/// </summary>
/// <remarks>
/// The API always reports every severity and channel combination rather than only the rows
/// that happen to be stored: an absent row is enabled by default, and a client configuring
/// its switches needs to see the effective answer for each combination, not the storage
/// detail behind it.
/// </remarks>
public sealed record NotificationPreferenceDto(
    string Severity,
    string Channel,
    bool Enabled);
