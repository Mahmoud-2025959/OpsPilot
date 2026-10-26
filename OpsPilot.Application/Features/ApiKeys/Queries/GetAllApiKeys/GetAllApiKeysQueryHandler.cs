using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.ApiKeys.Queries.GetAllApiKeys;

public sealed class GetAllApiKeysQueryHandler
    : IRequestHandler<GetAllApiKeysQuery, PagedResponse<ApiKeyDto>>
{
    private readonly IApiKeyRepository _repository;
    private readonly ISecretProtector _protector;

    public GetAllApiKeysQueryHandler(IApiKeyRepository repository, ISecretProtector protector)
    {
        _repository = repository;
        _protector = protector;
    }

    public async Task<PagedResponse<ApiKeyDto>> Handle(
        GetAllApiKeysQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, totalCount) = await _repository.GetPagedAsync(page, pageSize, cancellationToken);

        // The secret is stored encrypted and read back here, which is the point of storing it:
        // an operator who lost a key reads it off the list instead of having to reissue one.
        var dtos = items
            .Select(k => k.ToDto(_protector.Unprotect(k.KeyCiphertext)))
            .ToList();

        return PagedResponse<ApiKeyDto>.Create(dtos, page, pageSize, totalCount);
    }
}
