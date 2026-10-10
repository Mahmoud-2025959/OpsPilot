namespace OpsPilot.Contracts.Auth;

public sealed record AuthResponseDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset TokenExpiresAt,
    DateTimeOffset RefreshExpiresAt);
