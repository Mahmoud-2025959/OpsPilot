using OpsPilot.Contracts.Permissions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class PermissionMappings
{
    public static PermissionDto ToDto(this Permission permission) =>
        new(permission.Id,
            permission.Name,
            permission.Category,
            permission.Description);
}
