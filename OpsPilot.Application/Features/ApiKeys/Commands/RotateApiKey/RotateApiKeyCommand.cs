using MediatR;
using OpsPilot.Contracts.ApiKeys;

namespace OpsPilot.Application.Features.ApiKeys.Commands.RotateApiKey;

/// <summary>
/// Issues a new secret for a key that already exists. The previous secret stops working as
/// soon as the rotation is saved.
/// </summary>
/// <remarks>
/// Rotation is how a key that has no readable secret gets one: the hash of a key that was
/// issued before the secret began being stored cannot be reversed into the secret it was made
/// from, so the only way forward is a new secret on the same row — keeping the name, the
/// expiry and the incident history attached to it.
/// </remarks>
public sealed record RotateApiKeyCommand(Guid Id) : IRequest<CreatedApiKeyDto>;
