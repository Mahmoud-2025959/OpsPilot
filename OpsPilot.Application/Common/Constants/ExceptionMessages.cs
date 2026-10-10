namespace OpsPilot.Application.Common.Constants;

public static class ExceptionMessages
{
    public const string InvalidEmailOrPassword = "Invalid email or password.";
    public const string AccountInactive = "This account is inactive.";
    public const string EmailAlreadyExists = "An account with this email already exists.";
    public const string CurrentPasswordIncorrect = "The current password is incorrect.";
    public const string InvalidOrExpiredRefreshToken = "The refresh token is invalid or has expired.";
    public const string RefreshTokenRevoked = "The refresh token has been revoked.";
    public const string RefreshTokenReuseDetected = "Refresh token reuse detected. All sessions have been revoked.";
    public const string RefreshTokenNotFound = "The refresh token was not found.";
    public const string RefreshTokenNotBelongToUser = "The refresh token does not belong to this user.";

    public static string UserNotFound(Guid id) => $"User with id '{id}' was not found.";

    public static string IncidentNotFound(Guid id) => $"Incident with id '{id}' was not found.";

    public static string RolesDoNotExist(IEnumerable<string> roles) =>
        $"The following roles do not exist: {string.Join(", ", roles)}.";
}
