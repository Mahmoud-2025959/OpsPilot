using MediatR;
using OpsPilot.Contracts.Webhooks;

namespace OpsPilot.Application.Features.Webhooks.Commands.RotateWebhookSecret;

/// <summary>
/// Mints a new signing secret for a registered endpoint. The old secret stops signing
/// deliveries as soon as this is saved.
/// </summary>
public sealed record RotateWebhookSecretCommand(Guid Id) : IRequest<CreatedWebhookDto>;
