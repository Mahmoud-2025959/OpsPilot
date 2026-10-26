using System.Text.Json;
using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Json;
using OpsPilot.Application.Common.Messages;
using OpsPilot.Application.Features.Events.Commands.IngestEvent;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Integration.Handlers;

/// <summary>
/// Turns an event reported by another system into a timeline entry (plan, UC-06).
/// </summary>
/// <remarks>
/// The broker is a second door into the ingestion API, not a second way of doing things: the
/// same command runs and the same rules apply. The publisher proves who it is with an API
/// key, exactly as it does over HTTP, and the key's issuer stands in as the reporter of the
/// incident — an incident names a user, and a machine is not one.
/// </remarks>
public sealed class IngestedEventHandler : IIntegrationEventHandler
{
    private readonly IMediator _mediator;
    private readonly IApiKeyRepository _apiKeys;

    public IngestedEventHandler(IMediator mediator, IApiKeyRepository apiKeys)
    {
        _mediator = mediator;
        _apiKeys = apiKeys;
    }

    public string Type => IntegrationEventTypes.EventIngested;

    public async Task HandleAsync(
        IntegrationEventMessage message,
        CancellationToken cancellationToken = default)
    {
        var body = message.Payload.Deserialize<IngestedEventBody>(JsonDefaults.Options)
            ?? throw new UnprocessableEntityException(ExceptionMessages.IntegrationEventMalformed);

        if (!TryParseEnvironment(body.Environment, out var environment)
            || !TryParseSeverity(body.Severity, out var severity))
        {
            throw new UnprocessableEntityException(ExceptionMessages.IntegrationEventMalformed);
        }

        // The key is the publisher's identity. It is read through the tenant filter, so a key
        // belonging to another company is not found here: the envelope and the key have to
        // agree about who the event belongs to.
        var apiKey = await _apiKeys.GetByIdAsync(body.ApiKeyId, cancellationToken);

        if (apiKey is null || !apiKey.IsUsable())
        {
            throw new UnprocessableEntityException(ExceptionMessages.ApiKeyUnusable);
        }

        if (!Guid.TryParse(apiKey.CreatedBy, out var reportedBy) || reportedBy == Guid.Empty)
        {
            throw new UnprocessableEntityException(ExceptionMessages.ApiKeyNotAssociated);
        }

        await _mediator.Send(
            new IngestEventCommand(
                message.TenantId,
                apiKey.Id,
                reportedBy,
                body.Service,
                environment,
                severity,
                body.Message,
                body.OccurredAt),
            cancellationToken);
    }

    private static bool TryParseEnvironment(string? value, out EnvironmentType environment) =>
        Enum.TryParse(value, ignoreCase: true, out environment) && Enum.IsDefined(environment);

    private static bool TryParseSeverity(string? value, out Severity severity) =>
        Enum.TryParse(value, ignoreCase: true, out severity) && Enum.IsDefined(severity);
}

/// <summary>The body of an event reported over the broker.</summary>
/// <param name="ApiKeyId">The key the publisher is acting under.</param>
/// <param name="Service">The service the event is about, by name.</param>
/// <param name="Environment">The environment the service runs in.</param>
/// <param name="Severity">How bad it is.</param>
/// <param name="Message">The event itself.</param>
/// <param name="OccurredAt">When it happened, or null for now.</param>
internal sealed record IngestedEventBody(
    Guid ApiKeyId,
    string Service,
    string Environment,
    string Severity,
    string Message,
    DateTimeOffset? OccurredAt);
