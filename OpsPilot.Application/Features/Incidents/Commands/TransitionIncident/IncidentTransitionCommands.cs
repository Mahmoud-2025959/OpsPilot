using MediatR;

namespace OpsPilot.Application.Features.Incidents.Commands.TransitionIncident;

public sealed record AcknowledgeIncidentCommand(Guid IncidentId, Guid ActorId) : IRequest;
public sealed record StartInvestigatingIncidentCommand(Guid IncidentId, Guid ActorId) : IRequest;
public sealed record MitigateIncidentCommand(Guid IncidentId, Guid ActorId) : IRequest;
public sealed record ResolveIncidentCommand(Guid IncidentId, Guid ActorId) : IRequest;
public sealed record CloseIncidentCommand(Guid IncidentId, Guid ActorId) : IRequest;
public sealed record ReopenIncidentCommand(Guid IncidentId, Guid ActorId) : IRequest;
