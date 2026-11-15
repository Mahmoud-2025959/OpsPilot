using MediatR;

namespace OpsPilot.Application.Features.Webhooks.Commands.DeleteWebhook;

/// <summary>
/// Retires a webhook endpoint.
/// </summary>
/// <remarks>
/// Retiring, not erasing: the calls already made still point at the endpoint they were made
/// to, and a row that disappeared would leave them pointing at nothing.
/// </remarks>
public sealed record DeleteWebhookCommand(Guid Id) : IRequest;
