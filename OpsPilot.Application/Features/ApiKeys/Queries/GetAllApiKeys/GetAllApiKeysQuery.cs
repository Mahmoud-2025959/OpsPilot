using MediatR;
using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.ApiKeys.Queries.GetAllApiKeys;

public sealed record GetAllApiKeysQuery(int Page = 1, int PageSize = 20)
    : IRequest<PagedResponse<ApiKeyDto>>;
