using MediatR;

namespace OpsPilot.Application.Features.ApiKeys.Commands.RevokeApiKey;

/// <summary>
/// Revokes a key. Revocation is a state change, not a delete: the row stays so that events it
/// already ingested keep pointing at something real (ERD §3, "حوادث").
/// </summary>
public sealed record RevokeApiKeyCommand(Guid Id) : IRequest;
