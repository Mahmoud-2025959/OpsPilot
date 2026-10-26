namespace OpsPilot.Application.Common.Constants;

/// <summary>
/// The action names recorded on the audit trail (plan, FR-18).
/// </summary>
/// <remarks>
/// These are stored as data and shown in the audit view, so they are named after the event
/// rather than the endpoint that caused it: a trail reader should be able to follow what
/// happened without knowing the API's routing. Keep the set small — an action that nobody can
/// filter on or explain is noise on a record that can never be deleted.
/// </remarks>
public static class AuditActions
{
    /// <summary>An incident's severity was retargeted, which also retargets its SLA window.</summary>
    public const string IncidentSeverityChanged = "Incident.SeverityChanged";

    /// <summary>An incident was given (or given a new) assignee.</summary>
    public const string IncidentAssigned = "Incident.Assigned";

    /// <summary>An incident moved along the state machine; the statuses ride in the value snapshots.</summary>
    public const string IncidentStatusChanged = "Incident.StatusChanged";

    /// <summary>A user's role set was replaced.</summary>
    public const string UserRoleChanged = "User.RoleChanged";

    /// <summary>A role was created, renamed or retired.</summary>
    public const string RoleChanged = "Role.Changed";

    /// <summary>A role's permission set was replaced.</summary>
    public const string RolePermissionsChanged = "Role.PermissionsChanged";

    /// <summary>An incident's retrospective was opened.</summary>
    public const string PostmortemCreated = "Postmortem.Created";

    /// <summary>The written body of a postmortem changed.</summary>
    public const string PostmortemUpdated = "Postmortem.Updated";

    /// <summary>A postmortem moved between Draft, InReview and Published.</summary>
    public const string PostmortemStatusChanged = "Postmortem.StatusChanged";

    /// <summary>The company was given a new name.</summary>
    public const string TenantRenamed = "Tenant.Renamed";

    /// <summary>The company was activated or deactivated.</summary>
    public const string TenantStatusChanged = "Tenant.StatusChanged";

    /// <summary>A webhook endpoint was registered.</summary>
    public const string WebhookCreated = "Webhook.Created";

    /// <summary>A webhook endpoint's destination or state changed.</summary>
    public const string WebhookUpdated = "Webhook.Updated";

    /// <summary>A webhook endpoint was retired.</summary>
    public const string WebhookDeleted = "Webhook.Deleted";

    /// <summary>A webhook endpoint's signing secret was replaced.</summary>
    public const string WebhookSecretRotated = "Webhook.SecretRotated";
}
