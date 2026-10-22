using OpsPilot.Contracts.Teams;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class TeamMappings
{
    public static TeamDto ToDto(this Team t) =>
        new(t.Id,
            t.TenantId,
            t.Name,
            t.Description,
            t.Members
                .OrderBy(m => m.JoinedAt)
                .Select(m => new TeamMemberDto(m.UserId, m.Role.ToString(), m.JoinedAt))
                .ToList(),
            t.CreatedAt,
            t.UpdatedAt);
}
