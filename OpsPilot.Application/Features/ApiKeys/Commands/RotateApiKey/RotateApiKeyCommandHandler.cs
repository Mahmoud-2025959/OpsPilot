using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Features.ApiKeys;
using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.ApiKeys.Commands.RotateApiKey;

public sealed class RotateApiKeyCommandHandler : IRequestHandler<RotateApiKeyCommand, CreatedApiKeyDto>
{
    private readonly IApiKeyRepository _repository;
    private readonly ApiKeyIssuer _issuer;

    public RotateApiKeyCommandHandler(IApiKeyRepository repository, ApiKeyIssuer issuer)
    {
        _repository = repository;
        _issuer = issuer;
    }

    public async Task<CreatedApiKeyDto> Handle(
        RotateApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        // The lookup is tenant-filtered, so a key belonging to another company is a 404 rather
        // than something this caller can rotate.
        var apiKey = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(ApiKey), request.Id);

        var issued = await _issuer.RotateAsync(apiKey, cancellationToken);

        return new CreatedApiKeyDto(
            issued.Entity.Id,
            issued.Entity.Name,
            issued.Entity.KeyPrefix,
            issued.RawKey,
            issued.Entity.ExpiresAt,
            issued.Entity.CreatedAt);
    }
}
