using MediatR;
using OpsPilot.Contracts.ApiKeys;

namespace OpsPilot.Application.Features.ApiKeys.Queries.GetApiKey;

/// <summary>
/// Reads one key, with the secret. The secret is included because recovering it is the point:
/// an operator who lost the value of a key needs it back, and the list endpoint is a page of
/// keys rather than the one they are looking at.
/// </summary>
public sealed record GetApiKeyQuery(Guid Id) : IRequest<ApiKeyDto>;
