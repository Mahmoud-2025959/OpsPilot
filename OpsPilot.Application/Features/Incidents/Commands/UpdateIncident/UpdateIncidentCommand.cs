using MediatR;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Incidents.Commands.UpdateIncident;

public sealed record UpdateIncidentCommand(
    Guid IncidentId,
    string Title,
    string Description,
    Severity Severity) : IRequest;
