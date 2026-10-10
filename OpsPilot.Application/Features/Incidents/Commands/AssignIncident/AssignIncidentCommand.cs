using MediatR;

namespace OpsPilot.Application.Features.Incidents.Commands.AssignIncident;

public sealed record AssignIncidentCommand(Guid IncidentId, Guid AssigneeId, Guid ActorId) : IRequest;
