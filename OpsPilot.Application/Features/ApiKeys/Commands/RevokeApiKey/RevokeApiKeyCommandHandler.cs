using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.ApiKeys.Commands.RevokeApiKey;

public sealed class RevokeApiKeyCommandHandler : IRequestHandler<RevokeApiKeyCommand>
{
    private readonly IApiKeyRepository _repository;

    public RevokeApiKeyCommandHandler(IApiKeyRepository repository) => _repository = repository;

    public async Task Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        // The lookup is tenant-filtered, so a key belonging to another company is a 404 rather
        // than something this caller can revoke.
        var apiKey = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(ApiKey), request.Id);

        apiKey.Revoke();

        await _repository.UpdateAsync(apiKey, cancellationToken);
    }
}
