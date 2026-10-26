namespace OpsPilot.Contracts.Teams;

public sealed record TeamMemberDto(Guid UserId, string Role, DateTimeOffset JoinedAt);

public sealed record TeamDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    IReadOnlyList<TeamMemberDto> Members,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
