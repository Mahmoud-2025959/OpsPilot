namespace OpsPilot.Contracts.Users;

public sealed record UserDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles);
