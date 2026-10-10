namespace OpsPilot.Application.Common.Constants;

public static class ValidationMessages
{
    public const string TenantIdRequired = "TenantId is required.";
    public const string EmailRequired = "Email is required.";
    public const string EmailInvalid = "A valid email address is required.";
    public const string DisplayNameRequired = "Display name is required.";
    public const string PasswordRequired = "Password is required.";
    public const string PasswordTooShort = "Password must be at least 6 characters long.";
    public const string ConfirmPasswordRequired = "Confirm password is required.";
    public const string PasswordsDoNotMatch = "Passwords do not match.";
    public const string CurrentPasswordRequired = "Current password is required.";
    public const string NewPasswordRequired = "New password is required.";
    public const string RefreshTokenRequired = "Refresh token is required.";
}
