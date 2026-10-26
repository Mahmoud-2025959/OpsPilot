using OpsPilot.Domain.Entities;

namespace OpsPilot.Domain.Identity;

/// <summary>
/// The default role/permission matrix applied when a tenant is provisioned (ERD §3, the
/// roles-and-permissions table in the plan).
/// </summary>
/// <remarks>
/// Owner holds every permission and is the only role that may change company-wide settings.
/// Admin holds everything except <see cref="PermissionNames.TenantSettings"/>. TeamLead and
/// Engineer can work incidents; Viewer can only read. API keys are not roles and are granted
/// <see cref="PermissionNames.EventsIngest"/> directly when a key is issued.
/// </remarks>
public static class RolePermissionDefaults
{
    private static readonly IReadOnlyList<string> OwnerPermissions = PermissionCatalog.Names
        .Where(n => !string.Equals(n, PermissionNames.EventsIngest, StringComparison.OrdinalIgnoreCase))
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToList();

    private static readonly IReadOnlyList<string> AdminPermissions =
    [
        PermissionNames.IncidentsRead,
        PermissionNames.IncidentsCreate,
        PermissionNames.IncidentsUpdate,
        PermissionNames.IncidentsAssign,
        PermissionNames.IncidentsComment,
        PermissionNames.IncidentsResolve,
        PermissionNames.IncidentsClose,
        PermissionNames.PostmortemsRead,
        PermissionNames.PostmortemsWrite,
        PermissionNames.PostmortemsPublish,
        PermissionNames.ServicesManage,
        PermissionNames.TeamsManage,
        PermissionNames.UsersManage,
        PermissionNames.RolesChange,
        PermissionNames.SlaManage,
        PermissionNames.ApiKeysManage,
        PermissionNames.NotificationsConfigure,
        PermissionNames.WebhooksRead,
        PermissionNames.WebhooksManage,
        PermissionNames.AuditRead,
        PermissionNames.AnalyticsRead
    ];

    private static readonly IReadOnlyList<string> TeamLeadPermissions =
    [
        PermissionNames.IncidentsRead,
        PermissionNames.IncidentsCreate,
        PermissionNames.IncidentsUpdate,
        PermissionNames.IncidentsAssign,
        PermissionNames.IncidentsComment,
        PermissionNames.IncidentsResolve,
        PermissionNames.IncidentsClose,
        PermissionNames.PostmortemsRead,
        PermissionNames.PostmortemsWrite,
        PermissionNames.PostmortemsPublish,
        PermissionNames.TeamsManage,
        PermissionNames.NotificationsConfigure,
        PermissionNames.WebhooksRead,
        PermissionNames.AnalyticsRead
    ];

    private static readonly IReadOnlyList<string> EngineerPermissions =
    [
        PermissionNames.IncidentsRead,
        PermissionNames.IncidentsCreate,
        PermissionNames.IncidentsUpdate,
        PermissionNames.IncidentsAssign,
        PermissionNames.IncidentsComment,
        PermissionNames.IncidentsResolve,
        PermissionNames.PostmortemsRead,
        PermissionNames.PostmortemsWrite,
        PermissionNames.AnalyticsRead,
        PermissionNames.WebhooksRead
    ];

    private static readonly IReadOnlyList<string> ViewerPermissions =
    [
        PermissionNames.IncidentsRead,
        PermissionNames.PostmortemsRead,
        PermissionNames.AnalyticsRead,
        PermissionNames.WebhooksRead
    ];

    /// <summary>The permissions granted to a system role, by role name.</summary>
    public static IReadOnlyList<string> For(string roleName) => roleName switch
    {
        SystemRoleNames.Owner => OwnerPermissions,
        SystemRoleNames.Admin => AdminPermissions,
        SystemRoleNames.TeamLead => TeamLeadPermissions,
        SystemRoleNames.Engineer => EngineerPermissions,
        SystemRoleNames.Viewer => ViewerPermissions,
        _ => []
    };
}
