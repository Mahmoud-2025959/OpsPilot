using MediatR;
using System.Text.Json;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Contracts.Events;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Events.Commands.IngestEvent;

/// <summary>
/// The heart of ingestion: turn an external event into a timeline entry and, when nothing is
/// open for it yet, an incident (plan, UC-06).
/// </summary>
/// <remarks>
/// Correlation first, creation second. An event that matches an open incident — same service,
/// same environment, same normalized message — is folded into it and only bumps the occurrence
/// count, so one outage never becomes a stream of duplicate incidents.
/// </remarks>
public sealed class IngestEventCommandHandler : IRequestHandler<IngestEventCommand, EventIngestResultDto>
{
    /// <summary>Matches the <c>Incidents.Title</c> column; a longer message spills into the description.</summary>
    private const int MaxTitleLength = 200;

    private readonly IServiceRepository _services;
    private readonly IIncidentRepository _incidents;
    private readonly ISlaPolicyRepository _slaPolicies;
    private readonly TimeProvider _timeProvider;

    public IngestEventCommandHandler(
        IServiceRepository services,
        IIncidentRepository incidents,
        ISlaPolicyRepository slaPolicies,
        TimeProvider timeProvider)
    {
        _services = services;
        _incidents = incidents;
        _slaPolicies = slaPolicies;
        _timeProvider = timeProvider;
    }

    public async Task<EventIngestResultDto> Handle(
        IngestEventCommand request,
        CancellationToken cancellationToken)
    {
        // The event names its service by (name, environment). An unregistered pair is a 422 and
        // is deliberately not created on the fly (plan, UC-06): auto-creating services would let
        // a typo silently pollute the catalogue.
        var service = await _services.GetByNameAsync(request.Service, request.Environment, cancellationToken)
            ?? throw new UnprocessableEntityException(
                ExceptionMessages.IngestedServiceUnknown(request.Service, request.Environment));

        var occurredAt = request.OccurredAt ?? _timeProvider.GetUtcNow();
        var correlationKey = CorrelationKey.Compute(service.Id, request.Environment, request.Message);

        var open = await _incidents.GetOpenByCorrelationKeyAsync(correlationKey, cancellationToken);

        if (open is not null)
        {
            open.RecordEvent(occurredAt);

            var repeat = IncidentEvent.Create(
                open.TenantId,
                open.Id,
                IncidentEventType.EventIngested,
                occurredAt: occurredAt,
                payload: request.Message,
                apiKeyId: request.ApiKeyId);

            await _incidents.UpdateAsync(open, cancellationToken);
            await _incidents.AddEventAsync(
                repeat,
                Announce(IntegrationEventTypes.IncidentEventAdded, repeat, open),
                cancellationToken);

            return new EventIngestResultDto(repeat.Id, open.Id, Correlated: true);
        }

        var incident = Incident.Create(
            request.TenantId,
            service.Id,
            TrimToTitle(request.Message),
            request.Message,
            request.Severity,
            request.ReportedBy,
            occurredAt,
            correlationKey);

        // The deadlines run from the moment the incident opened, so the policy for its severity
        // is attached now — the same rule the manual-create path follows (ERD §3).
        var policy = await _slaPolicies.GetBySeverityAsync(request.Severity, cancellationToken);

        if (policy is not null)
        {
            incident.ApplySlaPolicy(policy.Id, policy.ResponseWindow, policy.ResolutionWindow);
        }

        await _incidents.AddAsync(incident, cancellationToken);

        var first = IncidentEvent.Create(
            incident.TenantId,
            incident.Id,
            IncidentEventType.EventIngested,
            occurredAt: occurredAt,
            payload: request.Message,
            apiKeyId: request.ApiKeyId);

        await _incidents.AddEventAsync(
            first,
            Announce(IntegrationEventTypes.IncidentCreated, first, incident),
            cancellationToken);

        return new EventIngestResultDto(first.Id, incident.Id, Correlated: false);
    }

    /// <summary>
    /// Stages the integration message for an entry in the same save as the entry itself
    /// (plan, UC-06 step 7).
    /// </summary>
    /// <remarks>
    /// The payload names the two ids a consumer needs and nothing else: the body of the event is
    /// readable from the timeline, and copying it here would create a second version of it to
    /// keep in step.
    /// </remarks>
    private static OutboxMessage Announce(string type, IncidentEvent entry, Incident incident) =>
        OutboxMessage.Create(
            type,
            JsonSerializer.Serialize(new { eventId = entry.Id, incidentId = incident.Id }),
            incident.TenantId);

    private static string TrimToTitle(string message) =>
        message.Length <= MaxTitleLength ? message : message[..MaxTitleLength];
}
