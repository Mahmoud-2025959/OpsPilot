using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Incidents.Commands.UpdateIncident;

public sealed class UpdateIncidentCommandHandler : IRequestHandler<UpdateIncidentCommand>
{
    private readonly IIncidentRepository _repository;
    private readonly ISlaPolicyRepository _slaPolicies;
    private readonly IAuditLogWriter _audit;

    public UpdateIncidentCommandHandler(
        IIncidentRepository repository,
        ISlaPolicyRepository slaPolicies,
        IAuditLogWriter audit)
    {
        _repository = repository;
        _slaPolicies = slaPolicies;
        _audit = audit;
    }

    public async Task Handle(UpdateIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        var previousSeverity = incident.Severity;
        var severityChanged = previousSeverity != request.Severity;

        incident.UpdateDetails(request.Title, request.Description, request.Severity);

        // Changing the severity retargets the SLA (ERD §3, "تغيير الخطورة يعيد حساب مواعيد SLA").
        if (severityChanged)
        {
            var policy = await _slaPolicies.GetBySeverityAsync(request.Severity, cancellationToken);

            if (policy is not null)
            {
                incident.ApplySlaPolicy(
                    policy.Id,
                    TimeSpan.FromMinutes(policy.ResponseMinutes),
                    TimeSpan.FromMinutes(policy.ResolutionMinutes));
            }
        }

        await _repository.UpdateAsync(incident, cancellationToken);

        // Only the severity move is audited, and only when it actually moved: the same edit may
        // be re-saved with a corrected title a dozen times, and the trail exists to answer "who
        // raised this to Critical", not to mirror every save (FR-18).
        if (severityChanged)
        {
            await _audit.WriteAsync(
                new AuditEntry(
                    AuditActions.IncidentSeverityChanged,
                    nameof(Incident),
                    incident.Id.ToString(),
                    OldValues: new { severity = previousSeverity.ToString() },
                    NewValues: new { severity = incident.Severity.ToString() }),
                cancellationToken);
        }
    }
}
