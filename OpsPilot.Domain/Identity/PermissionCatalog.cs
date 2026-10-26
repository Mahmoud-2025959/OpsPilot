namespace OpsPilot.Domain.Identity;

/// <summary>One row of the global permission catalogue.</summary>
/// <param name="Name">The stable permission string, e.g. <c>incidents.read</c>.</param>
/// <param name="Category">Grouping used by the UI when rendering the permission matrix.</param>
/// <param name="Description">Human-readable explanation shown next to the checkbox.</param>
public sealed record PermissionDefinition(string Name, string Category, string? Description = null);

/// <summary>
/// The complete, fixed set of permissions the system understands.
/// </summary>
/// <remarks>
/// This catalogue is deliberately code, not data: <see cref="Entities.Permission"/> is the
/// one table that is global rather than tenant-scoped (ERD §1), and it is seeded from here
/// whenever a tenant is provisioned. Keeping the list in the domain means a permission can
/// never be referenced by a policy without also being seeded for every tenant.
/// </remarks>
public static class PermissionCatalog
{
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(PermissionNames.IncidentsRead, "Incidents", "View incidents and their timelines."),
        new(PermissionNames.IncidentsCreate, "Incidents", "Open an incident manually."),
        new(PermissionNames.IncidentsUpdate, "Incidents", "Edit an incident and move it between active states."),
        new(PermissionNames.IncidentsAssign, "Incidents", "Assign an incident to a responder."),
        new(PermissionNames.IncidentsComment, "Incidents", "Add comments to an incident."),
        new(PermissionNames.IncidentsResolve, "Incidents", "Mark an incident resolved."),
        new(PermissionNames.IncidentsClose, "Incidents", "Close a resolved incident."),

        new(PermissionNames.PostmortemsRead, "Postmortems", "Read postmortems and their content."),
        new(PermissionNames.PostmortemsWrite, "Postmortems", "Draft, edit and send a postmortem for review."),
        new(PermissionNames.PostmortemsPublish, "Postmortems", "Publish a reviewed postmortem."),

        new(PermissionNames.ServicesManage, "Catalogue", "Create and edit services."),
        new(PermissionNames.TeamsManage, "Catalogue", "Create teams and manage their members."),
        new(PermissionNames.UsersManage, "Administration", "Invite, edit, activate and deactivate users."),
        new(PermissionNames.RolesChange, "Administration", "Change the roles assigned to a user."),
        new(PermissionNames.SlaManage, "Administration", "Configure the response and resolution targets."),
        new(PermissionNames.ApiKeysManage, "Administration", "Issue and revoke ingestion API keys."),
        new(PermissionNames.NotificationsConfigure, "Administration", "Configure notification channels and preferences."),
        new(PermissionNames.WebhooksRead, "Administration", "Read webhook endpoints and their deliveries."),
        new(PermissionNames.WebhooksManage, "Administration", "Register, edit and retire webhook endpoints."),
        new(PermissionNames.AuditRead, "Governance", "Read the audit log."),
        new(PermissionNames.AnalyticsRead, "Governance", "Read the dashboard and analytics summaries."),
        new(PermissionNames.TenantSettings, "Governance", "Change company-wide settings."),

        new(PermissionNames.EventsIngest, "Ingestion", "Send external events (non-human API keys only).")
    ];

    /// <summary>Permission names, for fast lookup and seeding.</summary>
    public static IReadOnlySet<string> Names { get; } =
        All.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="name"/> is a permission this build knows about.</summary>
    public static bool Contains(string name) => Names.Contains(name);
}
