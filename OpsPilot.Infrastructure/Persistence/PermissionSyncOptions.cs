namespace OpsPilot.Infrastructure.Persistence;

/// <summary>Settings for the startup synchronization of the permission catalogue.</summary>
public sealed class PermissionSyncOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "Permissions";

    /// <summary>
    /// False keeps the synchronization out of the host altogether. Test hosts set it: every
    /// host they build would otherwise make a pass over every company in the shared fixture
    /// database, and the pass is covered by its own test instead.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
