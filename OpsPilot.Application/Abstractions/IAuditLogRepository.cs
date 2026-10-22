using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages the trail newest-first, narrowed by any combination of the supplied filters.
    /// </summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPagedAsync(
        string? action,
        string? entityType,
        string? entityId,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
