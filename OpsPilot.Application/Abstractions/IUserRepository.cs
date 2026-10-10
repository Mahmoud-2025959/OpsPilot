using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

public interface IUserRepository
{
    Task<Guid> AddAsync(User user, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        string? search,
        string? role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    Task DeleteAsync(User user, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetRolesByNamesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default);
}
