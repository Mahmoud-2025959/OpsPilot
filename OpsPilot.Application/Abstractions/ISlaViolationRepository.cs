using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Stores the record that an incident missed one of its SLA deadlines (plan, UC-12).
/// </summary>
public interface ISlaViolationRepository
{
    /// <summary>
    /// The breaches already on record for any of <paramref name="incidentIds"/>, so a sweep can
    /// tell what it has still to report. The tenant comes from the ambient scope.
    /// </summary>
    Task<IReadOnlyList<SlaViolation>> GetForIncidentsAsync(
        IReadOnlyCollection<Guid> incidentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the breach and its timeline entry in one save: the row that says a deadline was
    /// missed and the line that explains it are one fact, and a violation nobody can trace back
    /// to a moment on the timeline would be a dead end for whoever reads it.
    /// </summary>
    Task AddAsync(
        SlaViolation violation,
        IncidentEvent timelineEntry,
        CancellationToken cancellationToken = default);
}
