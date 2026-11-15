using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Reads and updates the transactional outbox (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// The dispatcher works across companies on purpose: the queue is drained in the order it was
/// filled regardless of who filled it, and a company being idle is no reason for its messages to
/// wait. That cross-tenant reach is what <see cref="GetPendingAsync"/> has to express explicitly.
/// </remarks>
public interface IOutboxRepository
{
    /// <summary>The messages still waiting to be published, oldest first.</summary>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>Commits the processed/failed state a pass has just given its messages.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
