using OpsPilot.Contracts.Tenants;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class TenantMappings
{
    public static TenantDto ToDto(this Tenant t) =>
        new(t.Id,
            t.Name,
            t.Slug,
            t.IsActive,
            t.CreatedAt,
            t.UpdatedAt);
}
