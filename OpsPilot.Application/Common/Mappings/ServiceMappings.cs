using OpsPilot.Contracts.Services;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class ServiceMappings
{
    public static ServiceDto ToDto(this Service s) =>
        new(s.Id,
            s.TenantId,
            s.Name,
            s.Slug,
            s.Environment.ToString(),
            s.Description,
            s.RepositoryUrl,
            s.OwnerTeamId,
            s.IsActive,
            s.CreatedAt,
            s.UpdatedAt);
}
