using System.Text.Json;
using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.SlaViolations.Commands.DetectSlaViolations;

/// <summary>
/// Finds the incidents whose SLA deadlines have passed without the matching step being taken,
/// and reports each one once (plan, UC-12).
/// </summary>
/// <remarks>
/// <para>
/// A breach is a <see cref="SlaViolation"/> row — the unique index on
/// <c>(IncidentId, SlaType)</c> is what makes the sweep idempotent, since a second recording of
/// the same breach cannot be stored — a timeline entry, and a notification to the people the
/// incident concerns.
/// </para>
/// <para>
/// The response deadline is missed when nobody has acknowledged the incident; the resolution
/// deadline when nobody has resolved it. Both are judged against the same instant, so one
/// sweep can report both for the same incident and a test can reason about them together.
/// </para>
/// </remarks>
public sealed class DetectSlaViolationsCommandHandler(
    IIncidentRepository incidents,
    ISlaViolationRepository violations,
    IServiceRepository services,
    INotificationService notifications)
    : IRequestHandler<DetectSlaViolationsCommand, int>
{
    public async Task<int> Handle(DetectSlaViolationsCommand request, CancellationToken cancellationToken)
    {
        var now = request.Now;

        var overdue = await incidents.GetSlaOverdueAsync(now, cancellationToken);

        if (overdue.Count == 0)
        {
            return 0;
        }

        // Loaded for the whole batch rather than incident by incident: a company with a backlog
        // of breaches is exactly when this sweep matters most, and exactly when a per-row
        // lookup would be slowest.
        var recorded = (await violations.GetForIncidentsAsync(
                overdue.Select(incident => incident.Id).ToList(),
                cancellationToken))
            .Select(violation => (violation.IncidentId, violation.SlaType))
            .ToHashSet();

        // The escalation targets are resolved once for the batch too, for the same reason.
        var managers = await services.GetServiceManagersAsync(
            overdue.Select(incident => incident.ServiceId).Distinct().ToList(),
            cancellationToken);

        var detected = 0;

        foreach (var incident in overdue)
        {
            var escalateTo = managers.TryGetValue(incident.ServiceId, out var leads)
                ? leads
                : [];

            if (IsResponseMissed(incident, now) && recorded.Add((incident.Id, SlaType.Response)))
            {
                await ReportAsync(
                    incident,
                    SlaType.Response,
                    incident.ResponseDueAt!.Value,
                    now,
                    escalateTo,
                    cancellationToken);

                detected++;
            }

            if (IsResolutionMissed(incident, now) && recorded.Add((incident.Id, SlaType.Resolution)))
            {
                await ReportAsync(
                    incident,
                    SlaType.Resolution,
                    incident.ResolutionDueAt!.Value,
                    now,
                    escalateTo,
                    cancellationToken);

                detected++;
            }
        }

        return detected;
    }

    private static bool IsResponseMissed(Incident incident, DateTimeOffset now)
        => incident.AcknowledgedAt is null
           && incident.ResponseDueAt is { } due
           && due < now;

    private static bool IsResolutionMissed(Incident incident, DateTimeOffset now)
        => incident.ResolvedAt is null
           && incident.ResolutionDueAt is { } due
           && due < now;

    private async Task ReportAsync(
        Incident incident,
        SlaType slaType,
        DateTimeOffset dueAt,
        DateTimeOffset now,
        IReadOnlyList<Guid> escalateTo,
        CancellationToken cancellationToken)
    {
        var window = slaType == SlaType.Response ? "response" : "resolution";

        var violation = SlaViolation.Create(incident.TenantId, incident.Id, slaType, dueAt, now);

        // The due time is carried on the entry so the timeline explains the breach on its own,
        // without a reader having to join back to the violation row to work out what happened.
        var timelineEntry = IncidentEvent.Create(
            incident.TenantId,
            incident.Id,
            IncidentEventType.SlaViolationDetected,
            occurredAt: now,
            payload: JsonSerializer.Serialize(new { slaType = slaType.ToString(), dueAt }));

        await violations.AddAsync(violation, timelineEntry, cancellationToken);

        // The person carrying the incident and the person who reported it both need to hear
        // that the clock ran out. When they are the same person the set keeps it to one
        // notification rather than two identical ones.
        var recipients = new HashSet<Guid> { incident.CreatedByUserId };

        if (incident.AssignedTo is { } assignee)
        {
            recipients.Add(assignee);
        }

        await notifications.NotifyAsync(
            recipients,
            NotificationType.SlaViolation,
            incident.Severity,
            title: $"SLA missed ({window}): {incident.Title}",
            body: $"Incident '{incident.Title}' missed its {window} deadline ({dueAt:u}).",
            incident.Id,
            cancellationToken);

        // A breach that nobody is picking up has to reach the people accountable for the
        // service, not just the people already looking at the incident (plan, UC-12
        // follow-up). They are notified as a separate, clearly-labelled message so the
        // escalation is distinguishable from the ordinary breach notice - and the same set
        // dedup applies, so a lead who is also the assignee is not told twice.
        if (escalateTo.Count > 0)
        {
            var escalated = escalateTo.Where(userId => !recipients.Contains(userId)).ToList();

            if (escalated.Count > 0)
            {
                await notifications.NotifyAsync(
                    escalated,
                    NotificationType.SlaViolationEscalated,
                    incident.Severity,
                    title: $"SLA escalation ({window}): {incident.Title}",
                    body: $"Incident '{incident.Title}' is still open and has passed its "
                        + $"{window} deadline ({dueAt:u}). It has been escalated to the "
                        + "owning team's lead.",
                    incident.Id,
                    cancellationToken);
            }
        }
    }
}
