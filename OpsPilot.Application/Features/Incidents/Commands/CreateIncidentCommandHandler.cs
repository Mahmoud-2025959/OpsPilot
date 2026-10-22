using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Incidents.Commands;

public sealed class CreateIncidentCommandHandler : IRequestHandler<CreateIncidentCommand, Guid>
{
    private readonly IIncidentRepository _repository;
    private readonly IServiceRepository _services;
    private readonly ISlaPolicyRepository _slaPolicies;

    public CreateIncidentCommandHandler(
        IIncidentRepository repository,
        IServiceRepository services,
        ISlaPolicyRepository slaPolicies)
    {
        _repository = repository;
        _services = services;
        _slaPolicies = slaPolicies;
    }

    public async Task<Guid> Handle(CreateIncidentCommand request, CancellationToken cancellationToken)
    {
        // ServiceId is a foreign key. An id that names nothing -- or names a service belonging
        // to another company, which the bare constraint would accept -- has to be refused here,
        // or it surfaces as a constraint violation from the save path instead of a 404. The
        // lookup is tenant-filtered, so the cross-tenant case is refused as well.
        if (await _services.GetByIdAsync(request.ServiceId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Service), request.ServiceId);
        }

        var incident = Incident.Create(
            request.TenantId,
            request.ServiceId,
            request.Title,
            request.Description,
            request.Severity,
            request.CreatedBy,
            DateTimeOffset.UtcNow,
            request.CorrelationKey);

        // The deadlines are measured from the moment the incident was opened, so the policy for
        // its severity is attached at creation. Without this an incident would carry no SLA
        // until someone happened to edit its severity (ERD §3, the severity-change rule).
        var policy = await _slaPolicies.GetBySeverityAsync(request.Severity, cancellationToken);

        if (policy is not null)
        {
            incident.ApplySlaPolicy(
                policy.Id,
                TimeSpan.FromMinutes(policy.ResponseMinutes),
                TimeSpan.FromMinutes(policy.ResolutionMinutes));
        }

        return await _repository.AddAsync(incident, cancellationToken);
    }
}
