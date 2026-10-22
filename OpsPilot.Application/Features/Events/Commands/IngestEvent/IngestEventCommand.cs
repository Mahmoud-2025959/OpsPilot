using MediatR;
using OpsPilot.Contracts.Events;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Events.Commands.IngestEvent;

/// <summary>
/// Records an operational event that arrived over the ingestion API (plan, UC-06).
/// </summary>
/// <param name="TenantId">The company that owns the API key; never taken from the request body.</param>
/// <param name="ApiKeyId">The key that authenticated the call, stamped on the timeline entry.</param>
/// <param name="ReportedBy">
/// The user the key belongs to. An incident names its reporter by user id, and a machine is not
/// a user, so the key's issuer stands in for it.
/// </param>
public sealed record IngestEventCommand(
    Guid TenantId,
    Guid ApiKeyId,
    Guid ReportedBy,
    string Service,
    EnvironmentType Environment,
    Severity Severity,
    string Message,
    DateTimeOffset? OccurredAt = null) : IRequest<EventIngestResultDto>;
