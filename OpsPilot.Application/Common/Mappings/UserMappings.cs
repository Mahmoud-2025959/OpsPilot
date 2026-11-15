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
            user.Roles.Select(r => r.Name ?? string.Empty).OrderBy(n => n).ToList(),
            user.Roles
                .Where(r => !r.IsDeleted)
                .SelectMany(r => r.Permissions)
                .Select(p => p.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList());
    }

    public static CurrentUserDto ToCurrentUserDto(this User user)
    {
        return new CurrentUserDto(
            user.Id,
            user.TenantId,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.Roles.Select(r => r.Name ?? string.Empty).OrderBy(n => n).ToList(),
            user.Roles
                .SelectMany(r => r.Permissions)
                .Select(p => p.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList());
    }
}
