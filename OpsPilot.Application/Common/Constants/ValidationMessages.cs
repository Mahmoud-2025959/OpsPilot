namespace OpsPilot.Application.Common.Constants;

public static class ValidationMessages
{
    public const string TenantIdRequired = "TenantId is required.";
    public const string CompanyNameRequired = "Company name is required.";
    public const string SlugInvalid = "The company handle may contain lowercase letters, digits and dashes only.";
    public const string EmailRequired = "Email is required.";
    public const string EmailInvalid = "A valid email address is required.";
    public const string DisplayNameRequired = "Display name is required.";
    public const string PasswordRequired = "Password is required.";
    public const string PasswordTooShort = "Password must be at least 8 characters long.";
    public const string ConfirmPasswordRequired = "Confirm password is required.";
    public const string PasswordsDoNotMatch = "Passwords do not match.";
    public const string CurrentPasswordRequired = "Current password is required.";
    public const string NewPasswordRequired = "New password is required.";
    public const string RefreshTokenRequired = "Refresh token is required.";

    // Services (catalogue).
    public const string ServiceNameRequired = "Service name is required.";
    public const string ServiceEnvironmentRequired = "An environment is required.";
    public const string ServiceEnvironmentInvalid =
        "A valid environment is required. Expected one of: Production, Staging, Development.";
    public const string ServiceSlugInvalid =
        "The service slug may contain lowercase letters, digits and dashes only.";

    // Teams.
    public const string TeamNameRequired = "Team name is required.";
    public const string TeamMemberUserIdRequired = "A user id is required.";
    public const string TeamMemberRoleInvalid =
        "A valid team role is required. Expected one of: Lead, Member.";

    // Roles and status.
    public const string UserRolesRequired = "At least one role is required.";

    // Roles and permissions.
    public const string RoleNameRequired = "Role name is required.";
    public const string RolePermissionNameRequired = "A permission name is required.";

    // Ingestion API keys.
    public const string ApiKeyNameRequired = "An API key name is required.";
    public const string ApiKeyExpiryMustBeInTheFuture = "The expiry must be in the future.";
    public const string ApiKeyIdRequired = "An API key id is required.";

    // Event ingestion.
    public const string IngestedServiceRequired = "A service name is required.";
    public const string IngestedMessageRequired = "An event message is required.";
    public const string IngestedSeverityInvalid =
        "A valid severity is required. Expected one of: Critical, High, Medium, Low.";

    // Notification preferences.
    public const string NotificationPreferencesRequired = "At least one notification preference is required.";
    public const string NotificationSeverityInvalid =
        "A valid severity is required. Expected one of: Critical, High, Medium, Low.";
    public const string NotificationChannelInvalid =
        "A valid channel is required. Expected one of: Email, InApp.";
    public const string NotificationPreferenceDuplicate =
        "Each severity and channel pair may be supplied at most once.";

    // Postmortems.
    public const string PostmortemTitleRequired = "A postmortem title is required.";
    public const string PostmortemSummaryRequired = "A postmortem summary is required.";

    // Webhooks.
    public const string WebhookUrlRequired = "A webhook URL is required.";
    public const string WebhookIdRequired = "A webhook endpoint id is required.";
    public const string WebhookUrlInvalid =
        "The webhook URL must be absolute, and must use HTTPS unless it points at the local machine.";

    // SLA policies.
    public const string SlaPoliciesRequired = "At least one SLA policy is required.";
    public const string SlaSeverityInvalid =
        "A valid severity is required. Expected one of: Critical, High, Medium, Low.";
    public const string SlaSeveritiesMustBeUnique = "Each severity may be supplied at most once.";
    public const string SlaResponseMustBePositive = "The response target must be greater than zero.";
    public const string SlaResolutionMustBePositive = "The resolution target must be greater than zero.";
    public const string SlaResolutionMustExceedResponse =
        "The resolution target must be greater than the response target.";
}
