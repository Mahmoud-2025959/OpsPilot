using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.ApiKeys.Queries.GetApiKey;

public sealed class GetApiKeyQueryHandler : IRequestHandler<GetApiKeyQuery, ApiKeyDto>
{
    private readonly IApiKeyRepository _repository;
    private readonly IApiKeySecretProtector _protector;

    public GetApiKeyQueryHandler(IApiKeyRepository repository, IApiKeySecretProtector protector)
    {
        _repository = repository;
        _protector = protector;
    }

    public async Task<ApiKeyDto> Handle(GetApiKeyQuery request, CancellationToken cancellationToken)
    {
        // The lookup is tenant-filtered, so a key belonging to another company is a 404 rather
        // than something this caller can read.
        var apiKey = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(ApiKey), request.Id);

        return apiKey.ToDto(_protector.Unprotect(apiKey.KeyCiphertext));
    }
}
