using OpsPilot.Contracts.Roles;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class RoleMappings
{
    public static RoleDto ToDto(this Role role) =>
        new(role.Id,
            role.TenantId,
            role.Name ?? string.Empty,
            role.Description,
            role.IsSystem,
            role.IsOwner,
            role.Permissions
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => p.ToDto())
                .ToList(),
            role.CreatedAt,
            role.UpdatedAt);
}
