using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Incidents.Commands.AssignIncident;

public sealed class AssignIncidentCommandHandler : IRequestHandler<AssignIncidentCommand>
{
    private readonly IIncidentRepository _repository;
    private readonly IUserRepository _users;
    private readonly IAuditLogWriter _audit;
    private readonly INotificationService _notifications;

    public AssignIncidentCommandHandler(
        IIncidentRepository repository,
        IUserRepository users,
        IAuditLogWriter audit,
        INotificationService notifications)
    {
        _repository = repository;
        _users = users;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task Handle(AssignIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        // AssignedTo is a foreign key. Looking the assignee up through the tenant-filtered
        // repository refuses both an id that names nothing and one that names a user in another
        // company -- the latter would otherwise be accepted by the constraint and leak an
        // incident across tenants.
        if (await _users.GetByIdAsync(request.AssigneeId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(User), request.AssigneeId);
        }

        var previousAssignee = incident.AssignedTo;

        incident.Assign(request.AssigneeId);

        var evt = IncidentEvent.Create(
            incident.TenantId,
            incident.Id,
            IncidentEventType.Assigned,
            occurredAt: DateTimeOffset.UtcNow,
            payload: request.AssigneeId.ToString());

        await _repository.UpdateAsync(incident, cancellationToken);
        await _repository.AddEventAsync(evt, cancellationToken);

        // Re-assigning an incident to the same person changes nothing, so it is not a sensitive
        // action and leaves no trace (FR-18) -- and it does not notify the assignee a second
        // time either, for the same reason.
        if (previousAssignee != request.AssigneeId)
        {
            await _audit.WriteAsync(
                new AuditEntry(
                    AuditActions.IncidentAssigned,
                    nameof(Incident),
                    incident.Id.ToString(),
                    OldValues: new { assignedTo = previousAssignee },
                    NewValues: new { assignedTo = request.AssigneeId }),
                cancellationToken);

            await _notifications.NotifyAsync(
                [request.AssigneeId],
                NotificationType.IncidentAssigned,
                incident.Severity,
                title: $"Incident assigned: {incident.Title}",
                body: $"You have been assigned incident '{incident.Title}'.",
                incident.Id,
                cancellationToken);
        }
    }
}
