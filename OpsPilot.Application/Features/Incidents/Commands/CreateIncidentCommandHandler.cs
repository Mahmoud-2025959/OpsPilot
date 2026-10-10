using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Features.Incidents.Commands;

public sealed class CreateIncidentCommandHandler : IRequestHandler<CreateIncidentCommand, Guid>
{
    private readonly IIncidentRepository _repository;

    public CreateIncidentCommandHandler(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = Incident.Create(
            request.TenantId,
            request.ServiceId,
            request.Title,
            request.Description,
            request.Severity,
            request.CreatedBy,
            DateTimeOffset.UtcNow,
            request.CorrelationKey);

        return await _repository.AddAsync(incident, cancellationToken);
    }
}