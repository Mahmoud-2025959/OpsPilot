using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// The permission catalogue (ERD §1).
/// </summary>
/// <remarks>
/// Unlike the roles, the catalogue is global: the same permissions apply to every company, so
/// these lookups deliberately cross the tenant boundary.
/// </remarks>
public interface IPermissionRepository
{
    /// <summary>Lists every permission the build knows about.</summary>
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the permissions with the given names. Names that are not in the catalogue are
    /// simply absent from the result, so the caller can report them.
    /// </summary>
    Task<IReadOnlyList<Permission>> GetByNamesAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default);
}
