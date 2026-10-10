using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Incidents.Commands.AssignIncident;

public sealed class AssignIncidentCommandHandler : IRequestHandler<AssignIncidentCommand>
{
    private readonly IIncidentRepository _repository;

    public AssignIncidentCommandHandler(IIncidentRepository repository) => _repository = repository;

    public async Task Handle(AssignIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        incident.Assign(request.AssigneeId);

        var evt = IncidentEvent.Create(
            incident.TenantId,
            incident.Id,
            IncidentEventType.Assigned,
            occurredAt: DateTimeOffset.UtcNow,
            payload: request.AssigneeId.ToString());

        await _repository.UpdateAsync(incident, cancellationToken);
        await _repository.AddEventAsync(evt, cancellationToken);
    }
}
