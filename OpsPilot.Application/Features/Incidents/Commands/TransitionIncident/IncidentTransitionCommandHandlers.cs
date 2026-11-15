using System.Text.Json;
using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Incidents.Commands.TransitionIncident;

/// <summary>
/// The shared shape of every state change: load the incident, apply the domain transition,
/// record the event, and hand back the incident in its new state.
/// </summary>
/// <remarks>
/// Collapsing the six handlers onto one routine is what keeps them from drifting — the load,
/// the event, and the returned DTO are identical for every edge, and only the domain call and
/// the event type differ. A rejected transition throws from the aggregate itself, so the
/// caller sees one exception type for every forbidden move.
/// </remarks>
internal static class IncidentTransition
{
    internal static async Task<IncidentDto> ApplyAsync(
        IIncidentRepository repository,
        IAuditLogWriter audit,
        INotificationService notifications,
        Guid incidentId,
        Guid actorId,
        string? note,
        IncidentEventType eventType,
        Action<Incident, DateTimeOffset> move,
        CancellationToken cancellationToken,
        NotificationType? notifyReporterOfType = null)
    {
        var incident = await repository.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), incidentId);

        var now = DateTimeOffset.UtcNow;
        var previousStatus = incident.Status;

        move(incident, now);

        var evt = IncidentEvent.Create(
            incident.TenantId,
            incident.Id,
            eventType,
            occurredAt: now,
            payload: BuildPayload(actorId, note));

        await repository.UpdateAsync(incident, cancellationToken);
        await repository.AddEventAsync(evt, cancellationToken);

        // UC-3 step 4: every move along the state machine lands in the trail as well as the
        // timeline, with the status it came from and the one it went to. The timeline says the
        // incident is now Resolved; the trail says Resolved came from Investigating, and who
        // asked for it (FR-18).
        await audit.WriteAsync(
            new AuditEntry(
                AuditActions.IncidentStatusChanged,
                nameof(Incident),
                incident.Id.ToString(),
                OldValues: new { status = previousStatus.ToString() },
                NewValues: new { status = incident.Status.ToString() }),
            cancellationToken);

        // Closing the loop with the reporter is only worth a notification on the edges where
        // the incident reaches its end — resolving and closing. The reporter asked for help,
        // so they are the one who needs to hear it arrived.
        if (notifyReporterOfType is { } notifyType)
        {
            await notifications.NotifyAsync(
                [incident.CreatedByUserId],
                notifyType,
                incident.Severity,
                title: $"{incident.Status}: {incident.Title}",
                body: $"Incident '{incident.Title}' is now {incident.Status}.",
                incident.Id,
                cancellationToken);
        }

        return incident.ToDto();
    }

    /// <summary>
    /// The acting user travels on every transition event; the free-text note rides along only
    /// when one was supplied, so rows written before notes existed keep their plain id payload.
    /// </summary>
    private static string BuildPayload(Guid actorId, string? note)
        => string.IsNullOrWhiteSpace(note)
            ? actorId.ToString()
            : JsonSerializer.Serialize(new { actorId, note = note.Trim() });
}

public sealed class AcknowledgeIncidentCommandHandler(
    IIncidentRepository repository,
    IAuditLogWriter audit,
    INotificationService notifications)
    : IRequestHandler<AcknowledgeIncidentCommand, IncidentDto>
{
    public Task<IncidentDto> Handle(AcknowledgeIncidentCommand request, CancellationToken cancellationToken)
        => IncidentTransition.ApplyAsync(
            repository,
            audit,
            notifications,
            request.IncidentId,
            request.ActorId,
            request.Note,
            IncidentEventType.Acknowledged,
            (incident, at) => incident.Acknowledge(at),
            cancellationToken);
}

public sealed class StartInvestigatingIncidentCommandHandler(
    IIncidentRepository repository,
    IAuditLogWriter audit,
    INotificationService notifications)
    : IRequestHandler<StartInvestigatingIncidentCommand, IncidentDto>
{
    public Task<IncidentDto> Handle(StartInvestigatingIncidentCommand request, CancellationToken cancellationToken)
        => IncidentTransition.ApplyAsync(
            repository,
            audit,
            notifications,
            request.IncidentId,
            request.ActorId,
            request.Note,
            IncidentEventType.InvestigationStarted,
            (incident, at) => incident.StartInvestigating(at),
            cancellationToken);
}

public sealed class MitigateIncidentCommandHandler(
    IIncidentRepository repository,
    IAuditLogWriter audit,
    INotificationService notifications)
    : IRequestHandler<MitigateIncidentCommand, IncidentDto>
{
    public Task<IncidentDto> Handle(MitigateIncidentCommand request, CancellationToken cancellationToken)
        => IncidentTransition.ApplyAsync(
            repository,
            audit,
            notifications,
            request.IncidentId,
            request.ActorId,
            request.Note,
            IncidentEventType.Mitigated,
            (incident, at) => incident.Mitigate(at),
            cancellationToken);
}

public sealed class ResolveIncidentCommandHandler(
    IIncidentRepository repository,
    IAuditLogWriter audit,
    INotificationService notifications)
    : IRequestHandler<ResolveIncidentCommand, IncidentDto>
{
    public Task<IncidentDto> Handle(ResolveIncidentCommand request, CancellationToken cancellationToken)
        => IncidentTransition.ApplyAsync(
            repository,
            audit,
            notifications,
            request.IncidentId,
            request.ActorId,
            request.Note,
            IncidentEventType.Resolved,
            (incident, at) => incident.Resolve(at),
            cancellationToken,
            notifyReporterOfType: NotificationType.IncidentResolved);
}

public sealed class CloseIncidentCommandHandler(
    IIncidentRepository repository,
    IAuditLogWriter audit,
    INotificationService notifications)
    : IRequestHandler<CloseIncidentCommand, IncidentDto>
{
    public Task<IncidentDto> Handle(CloseIncidentCommand request, CancellationToken cancellationToken)
        => IncidentTransition.ApplyAsync(
            repository,
            audit,
            notifications,
            request.IncidentId,
            request.ActorId,
            request.Note,
            IncidentEventType.Closed,
            (incident, at) => incident.Close(at),
            cancellationToken,
            notifyReporterOfType: NotificationType.IncidentClosed);
}

public sealed class ReopenIncidentCommandHandler(
    IIncidentRepository repository,
    IAuditLogWriter audit,
    INotificationService notifications)
    : IRequestHandler<ReopenIncidentCommand, IncidentDto>
{
    public Task<IncidentDto> Handle(ReopenIncidentCommand request, CancellationToken cancellationToken)
        => IncidentTransition.ApplyAsync(
            repository,
            audit,
            notifications,
            request.IncidentId,
            request.ActorId,
            request.Note,
            IncidentEventType.Reopened,
            (incident, at) => incident.Reopen(at),
            cancellationToken);
}
