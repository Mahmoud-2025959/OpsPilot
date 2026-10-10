using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Incidents.Commands.TransitionIncident;

public sealed class AcknowledgeIncidentCommandHandler : IRequestHandler<AcknowledgeIncidentCommand>
{
    private readonly IIncidentRepository _repository;

    public AcknowledgeIncidentCommandHandler(IIncidentRepository repository) => _repository = repository;

    public async Task Handle(AcknowledgeIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        var now = DateTimeOffset.UtcNow;
        incident.Acknowledge(now);

        var evt = IncidentEvent.Create(incident.TenantId, incident.Id, IncidentEventType.Acknowledged, occurredAt: now, payload: request.ActorId.ToString());

        await _repository.UpdateAsync(incident, cancellationToken);
        await _repository.AddEventAsync(evt, cancellationToken);
    }
}

public sealed class StartInvestigatingIncidentCommandHandler : IRequestHandler<StartInvestigatingIncidentCommand>
{
    private readonly IIncidentRepository _repository;

    public StartInvestigatingIncidentCommandHandler(IIncidentRepository repository) => _repository = repository;

    public async Task Handle(StartInvestigatingIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        var now = DateTimeOffset.UtcNow;
        incident.StartInvestigating(now);

        var evt = IncidentEvent.Create(incident.TenantId, incident.Id, IncidentEventType.InvestigationStarted, occurredAt: now, payload: request.ActorId.ToString());

        await _repository.UpdateAsync(incident, cancellationToken);
        await _repository.AddEventAsync(evt, cancellationToken);
    }
}

public sealed class MitigateIncidentCommandHandler : IRequestHandler<MitigateIncidentCommand>
{
    private readonly IIncidentRepository _repository;

    public MitigateIncidentCommandHandler(IIncidentRepository repository) => _repository = repository;

    public async Task Handle(MitigateIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        var now = DateTimeOffset.UtcNow;
        incident.Mitigate(now);

        var evt = IncidentEvent.Create(incident.TenantId, incident.Id, IncidentEventType.Mitigated, occurredAt: now, payload: request.ActorId.ToString());

        await _repository.UpdateAsync(incident, cancellationToken);
        await _repository.AddEventAsync(evt, cancellationToken);
    }
}

public sealed class ResolveIncidentCommandHandler : IRequestHandler<ResolveIncidentCommand>
{
    private readonly IIncidentRepository _repository;

    public ResolveIncidentCommandHandler(IIncidentRepository repository) => _repository = repository;

    public async Task Handle(ResolveIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        var now = DateTimeOffset.UtcNow;
        incident.Resolve(now);

        var evt = IncidentEvent.Create(incident.TenantId, incident.Id, IncidentEventType.Resolved, occurredAt: now, payload: request.ActorId.ToString());

        await _repository.UpdateAsync(incident, cancellationToken);
        await _repository.AddEventAsync(evt, cancellationToken);
    }
}

public sealed class CloseIncidentCommandHandler : IRequestHandler<CloseIncidentCommand>
{
    private readonly IIncidentRepository _repository;

    public CloseIncidentCommandHandler(IIncidentRepository repository) => _repository = repository;

    public async Task Handle(CloseIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        var now = DateTimeOffset.UtcNow;
        incident.Close(now);

        var evt = IncidentEvent.Create(incident.TenantId, incident.Id, IncidentEventType.Closed, occurredAt: now, payload: request.ActorId.ToString());

        await _repository.UpdateAsync(incident, cancellationToken);
        await _repository.AddEventAsync(evt, cancellationToken);
    }
}

public sealed class ReopenIncidentCommandHandler : IRequestHandler<ReopenIncidentCommand>
{
    private readonly IIncidentRepository _repository;

    public ReopenIncidentCommandHandler(IIncidentRepository repository) => _repository = repository;

    public async Task Handle(ReopenIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        var now = DateTimeOffset.UtcNow;
        incident.Reopen(now);

        var evt = IncidentEvent.Create(incident.TenantId, incident.Id, IncidentEventType.Reopened, occurredAt: now, payload: request.ActorId.ToString());

        await _repository.UpdateAsync(incident, cancellationToken);
        await _repository.AddEventAsync(evt, cancellationToken);
    }
}
