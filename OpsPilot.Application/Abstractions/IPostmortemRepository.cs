using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// The retrospectives of the ambient company (ERD §3).
/// </summary>
/// <remarks>
/// No method takes a tenant id: the global query filter has already narrowed this set to the
/// company the request was resolved for, which is what makes a cross-company read impossible
/// rather than merely discouraged.
/// </remarks>
public interface IPostmortemRepository
{
    Task<Guid> AddAsync(Postmortem postmortem, CancellationToken cancellationToken = default);

    Task<Postmortem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The incident's single retrospective, or null when nobody has opened one.</summary>
    Task<Postmortem?> GetByIncidentAsync(Guid incidentId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Postmortem postmortem, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Postmortem> Items, int TotalCount)> GetPagedAsync(
        PostmortemStatus? status,
        Guid? incidentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
