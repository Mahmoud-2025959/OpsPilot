using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Roles;

/// <summary>
/// Turns the permission names a request carries into the catalogue rows they name.
/// </summary>
/// <remarks>
/// Creating a role and changing what one grants are the same lookup with the same failure, so
/// it lives here rather than being written twice.
/// </remarks>
internal static class RolePermissionResolver
{
    /// <summary>
    /// Resolves the names to catalogue rows. A name the catalogue does not know is refused
    /// rather than ignored, so a typo cannot quietly produce a role that grants less than the
    /// caller asked for.
    /// </summary>
    public static async Task<IReadOnlyList<Permission>> ResolveAsync(
        IPermissionRepository permissions,
        IReadOnlyList<string>? names,
        CancellationToken cancellationToken = default)
    {
        if (names is null || names.Count == 0)
        {
            return [];
        }

        var distinct = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 0)
        {
            return [];
        }

        var found = await permissions.GetByNamesAsync(distinct, cancellationToken);

        var foundNames = found.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = distinct.Where(name => !foundNames.Contains(name)).ToList();

        if (missing.Count != 0)
        {
            throw new DomainException(ExceptionMessages.PermissionsDoNotExist(missing));
        }

        return found;
    }
}
