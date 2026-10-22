using MediatR;
using OpsPilot.Contracts.ApiKeys;

namespace OpsPilot.Application.Features.ApiKeys.Commands.CreateApiKey;

/// <summary>
/// Issues an ingestion key. The tenant comes from the caller, never from the body, and the raw
/// secret exists only in the returned DTO.
/// </summary>
public sealed record CreateApiKeyCommand(
    Guid TenantId,
    string Name,
    DateTimeOffset? ExpiresAt = null) : IRequest<CreatedApiKeyDto>;
