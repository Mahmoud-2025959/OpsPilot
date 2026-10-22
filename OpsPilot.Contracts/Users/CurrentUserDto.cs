namespace OpsPilot.Contracts.Users;

/// <summary>The signed-in user's own profile, including the permissions carried by the token.</summary>
public sealed record CurrentUserDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
