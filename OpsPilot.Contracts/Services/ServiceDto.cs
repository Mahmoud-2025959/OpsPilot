namespace OpsPilot.Contracts.Services;

public sealed record ServiceDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Slug,
    string Environment,
    string? Description,
    string? RepositoryUrl,
    Guid? OwnerTeamId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
