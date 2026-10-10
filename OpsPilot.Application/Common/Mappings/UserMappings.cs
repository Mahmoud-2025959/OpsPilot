using OpsPilot.Contracts.Users;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class UserMappings
{
    public static UserDto ToDto(this User user)
    {
        return new UserDto(
            user.Id,
            user.TenantId,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.IsActive,
            user.LastLoginAt,
            user.Roles.Select(r => r.Name ?? string.Empty).OrderBy(n => n).ToList());
    }
}
