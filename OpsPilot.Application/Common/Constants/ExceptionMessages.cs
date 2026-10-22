using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Common.Constants;

public static class ExceptionMessages
{
    public const string InvalidEmailOrPassword = "Invalid email or password.";
    public const string AccountInactive = "This account is inactive.";
    public const string EmailAlreadyExists = "An account with this email already exists.";
    public const string CompanySlugAlreadyExists = "A company with this handle already exists.";
    public const string CurrentPasswordIncorrect = "The current password is incorrect.";
    public const string InvalidOrExpiredRefreshToken = "The refresh token is invalid or has expired.";
    public const string RefreshTokenRevoked = "The refresh token has been revoked.";
    public const string RefreshTokenReuseDetected = "Refresh token reuse detected. All sessions have been revoked.";
    public const string RefreshTokenNotFound = "The refresh token was not found.";
    public const string RefreshTokenNotBelongToUser = "The refresh token does not belong to this user.";

    public static string UserNotFound(Guid id) => $"User with id '{id}' was not found.";

    public static string IncidentNotFound(Guid id) => $"Incident with id '{id}' was not found.";

    public const string ServiceAlreadyExists =
        "A service with this name already exists in this environment.";

    public const string TeamAlreadyExists = "A team with this name already exists.";

    public const string TeamMemberAlreadyExists = "That user is already a member of this team.";

    public const string LastOwnerCannotBeDemoted =
        "This is the last Owner of the company. Give the Owner role to someone else first.";

    public const string LastOwnerCannotBeDeactivated =
        "This is the last active Owner of the company. Give the Owner role to someone else first.";

    public const string LastOwnerCannotBeDeleted =
        "This is the last active Owner of the company. Give the Owner role to someone else before deleting the account.";

    public const string OwnerChangesBusy =
        "Another change to this company's owners is in progress. Please retry.";

    public static string RolesDoNotExist(IEnumerable<string> roles) =>
        $"The following roles do not exist: {string.Join(", ", roles)}.";

    public static string RoleNotFound(Guid id) => $"Role with id '{id}' was not found.";

    public const string RoleAlreadyExists = "A role with this name already exists in this company.";

    public static string RoleNameReserved(string name) =>
        $"'{name}' is a reserved role name and cannot be used for a custom role.";

    public static string SystemRoleCannotBeModified(string name) =>
        $"The system role '{name}' cannot be renamed or retired.";

    public const string OwnerPermissionsFrozen =
        "The Owner role's permissions are fixed and cannot be changed.";

    public static string PermissionsDoNotExist(IEnumerable<string> permissions) =>
        $"The following permissions do not exist: {string.Join(", ", permissions)}.";

    public static string PermissionsNotHeldByCaller(IEnumerable<string> permissions) =>
        $"You cannot grant permissions you do not hold yourself: {string.Join(", ", permissions)}.";

    public const string CallerNotInCompany =
        "The signed-in user is not part of this company, so it cannot grant permissions here.";

    public const string CannotGrantOwnerRole =
        "Only an Owner may assign the Owner role to another user.";

    public const string RoleStillAssigned =
        "This role is still assigned to users. Move them to another role before retiring it.";

    public const string ApiKeyNotFound = "The API key was not found.";

    public const string ApiKeyAlreadyExists = "An API key with this name already exists.";

    public const string ApiKeyPrefixBusy =
        "Could not allocate a unique API key prefix. Please retry.";

    public const string ApiKeyUnusable = "The API key is invalid, revoked or expired.";

    public const string ApiKeyNotAssociated = "This API key is not associated with an account.";

    /// <summary>
    /// The event names a service the company has not registered in that environment. The plan
    /// (UC-06) answers 422 here and deliberately does not create the service on the fly.
    /// </summary>
    public static string IngestedServiceUnknown(string service, EnvironmentType environment) =>
        $"No service named '{service}' is registered in the '{environment}' environment.";

    /// <summary>
    /// The tenant has no policy for the severity the caller tried to retune. Every company is
    /// seeded with one row per severity, so this only surfaces if provisioning was incomplete.
    /// </summary>
    public static string SlaPolicyNotFound(Severity severity) =>
        $"No SLA policy is configured for severity '{severity}'.";
}
