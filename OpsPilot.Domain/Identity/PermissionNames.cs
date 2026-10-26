namespace OpsPilot.Domain.Identity;

/// <summary>
/// Canonical permission names, exactly as agreed in the API contract and the permissions
/// matrix (ERD §3, "Identity و Tenancy"). They are the strings stored in the global
/// <see cref="Entities.Permission"/> catalogue and embedded as claims in the access token,
/// so they must never be renamed casually.
/// </summary>
public static class PermissionNames
{
    public const string IncidentsRead = "incidents.read";
    public const string IncidentsCreate = "incidents.create";
    public const string IncidentsUpdate = "incidents.update";
    public const string IncidentsAssign = "incidents.assign";
    public const string IncidentsComment = "incidents.comment";
    public const string IncidentsResolve = "incidents.resolve";
    public const string IncidentsClose = "incidents.close";

    public const string PostmortemsRead = "postmortems.read";
    public const string PostmortemsWrite = "postmortems.write";
    public const string PostmortemsPublish = "postmortems.publish";

    public const string ServicesManage = "services.manage";
    public const string TeamsManage = "teams.manage";
    public const string UsersManage = "users.manage";
    public const string RolesChange = "roles.change";
    public const string SlaManage = "sla.manage";
    public const string ApiKeysManage = "apikeys.manage";
    public const string NotificationsConfigure = "notifications.configure";
    public const string WebhooksRead = "webhooks.read";
    public const string WebhooksManage = "webhooks.manage";
    public const string AuditRead = "audit.read";
    public const string AnalyticsRead = "analytics.read";
    public const string TenantSettings = "tenant.settings";

    /// <summary>The single permission held by non-human API keys (ERD §3, "مفاتيح API").</summary>
    public const string EventsIngest = "events.ingest";
}
