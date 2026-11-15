using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.ApiKeys;
using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.ApiKeys.Commands.CreateApiKey;

public sealed class CreateApiKeyCommandHandler : IRequestHandler<CreateApiKeyCommand, CreatedApiKeyDto>
{
    /// <summary>How long a key lasts when the caller does not say. Long enough to be useful,
    /// short enough that a forgotten key does not live forever.</summary>
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(90);

    private readonly IApiKeyRepository _repository;
    private readonly ApiKeyIssuer _issuer;
    private readonly TimeProvider _timeProvider;

    public CreateApiKeyCommandHandler(
        IApiKeyRepository repository,
        ApiKeyIssuer issuer,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _issuer = issuer;
        _timeProvider = timeProvider;
    }

    public async Task<CreatedApiKeyDto> Handle(
        CreateApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        // Names are unique per tenant in the database. Checking first turns the would-be
        // constraint violation into a readable conflict.
        if (await _repository.NameExistsAsync(request.Name, null, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.ApiKeyAlreadyExists);
        }

        var expiresAt = request.ExpiresAt ?? _timeProvider.GetUtcNow().Add(DefaultLifetime);

        var issued = await _issuer.IssueAsync(
            request.TenantId,
            request.Name,
            expiresAt,
            cancellationToken);

        // The raw secret is handed back exactly once here — and again on every list from the
        // stored encrypted copy, which is what makes a lost key recoverable.
        return new CreatedApiKeyDto(
            issued.Entity.Id,
            issued.Entity.Name,
            issued.Entity.KeyPrefix,
            issued.RawKey,
            issued.Entity.ExpiresAt,
            issued.Entity.CreatedAt);
    }
}
