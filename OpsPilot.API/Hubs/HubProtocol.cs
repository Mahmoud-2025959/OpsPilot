namespace OpsPilot.API.Hubs;

/// <summary>
/// The names both ends of the live channel agree on: the events pushed to clients and the
/// groups those pushes are addressed to.
/// </summary>
/// <remarks>
/// They live here rather than as literals at each call site because a mismatch is silent — a
/// pushed event nobody is listening for, or a group nobody joined, simply never arrives. One
/// spelling on both sides turns that class of bug into a compile error.
/// </remarks>
internal static class HubProtocol
{
    /// <summary>An incident changed; sent to the company group.</summary>
    internal const string IncidentUpdated = "incidentUpdated";

    /// <summary>A notification was delivered; sent to the recipient's own group.</summary>
    internal const string NotificationCreated = "notificationCreated";

    /// <summary>A watcher spoke to the rest of their company.</summary>
    internal const string MessageReceived = "messageReceived";

    /// <summary>Longest message the live channel carries.</summary>
    internal const int MaxMessageLength = 2000;

    /// <summary>The personal group of one user, across every session they have open.</summary>
    internal static string UserGroup(Guid userId) => $"user:{userId}";

    /// <summary>The group everyone signed in to one company shares.</summary>
    internal static string TenantGroup(Guid tenantId) => $"tenant:{tenantId}";
}
