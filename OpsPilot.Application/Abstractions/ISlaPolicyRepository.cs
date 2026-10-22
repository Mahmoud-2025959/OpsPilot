using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Abstractions;

public interface ISlaPolicyRepository
{
    Task<IReadOnlyList<SlaPolicy>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SlaPolicy?> GetBySeverityAsync(Severity severity, CancellationToken cancellationToken = default);

    Task UpdateAsync(SlaPolicy policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a batch of retunes in a single save, so a policy set is never left half-updated.
    /// </summary>
    Task UpdateRangeAsync(
        IReadOnlyList<SlaPolicy> policies,
        CancellationToken cancellationToken = default);
}
