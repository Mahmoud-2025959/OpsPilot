using MediatR;
using OpsPilot.Contracts.Incidents;

namespace OpsPilot.Application.Features.Incidents.Commands.TransitionIncident;

/// <summary>
/// One command per edge of the incident state machine (ERD §3). Each returns the incident as
/// it stands <em>after</em> the move, so the caller does not have to re-read it, and carries
/// an optional human note recorded on the resulting timeline event.
/// </summary>
/// <remarks>
/// The permission required is not encoded here: it follows the target status and is checked
/// in the controller, where it can be answered by the authorization service.
/// </remarks>
public sealed record AcknowledgeIncidentCommand(Guid IncidentId, Guid ActorId, string? Note = null)
    : IRequest<IncidentDto>;

public sealed record StartInvestigatingIncidentCommand(Guid IncidentId, Guid ActorId, string? Note = null)
    : IRequest<IncidentDto>;

public sealed record MitigateIncidentCommand(Guid IncidentId, Guid ActorId, string? Note = null)
    : IRequest<IncidentDto>;

public sealed record ResolveIncidentCommand(Guid IncidentId, Guid ActorId, string? Note = null)
    : IRequest<IncidentDto>;

public sealed record CloseIncidentCommand(Guid IncidentId, Guid ActorId, string? Note = null)
    : IRequest<IncidentDto>;

public sealed record ReopenIncidentCommand(Guid IncidentId, Guid ActorId, string? Note = null)
    : IRequest<IncidentDto>;
