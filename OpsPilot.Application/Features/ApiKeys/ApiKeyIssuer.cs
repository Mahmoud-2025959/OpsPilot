using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.ApiKeys;

/// <summary>A freshly minted key: the row that was saved and the secret behind it.</summary>
public sealed record IssuedApiKey(ApiKey Entity, string RawKey);

/// <summary>
/// Mints ingestion keys and stores them: a random secret, the prefix that identifies it, the
/// hash authentication matches against, and the encrypted copy that lets the value be read
/// back later.
/// </summary>
/// <remarks>
/// Issuing and rotating are the same three steps with a different last one — insert a row
/// or update the existing one — so they share this class rather than each growing their own
/// copy of the prefix-collision retry.
/// </remarks>
public sealed class ApiKeyIssuer
{
    /// <summary>
    /// The stored prefix is globally unique; a clash is astronomically unlikely but cheap to
    /// sidestep, so a handful of fresh secrets are tried before giving up.
    /// </summary>
    private const int MaxPrefixAttempts = 5;

    private readonly IApiKeyRepository _repository;
    private readonly ISecretProtector _protector;

    public ApiKeyIssuer(IApiKeyRepository repository, ISecretProtector protector)
    {
        _repository = repository;
        _protector = protector;
    }

    /// <summary>Issues a new key and saves it.</summary>
    public async Task<IssuedApiKey> IssueAsync(
        Guid tenantId,
        string name,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var minted = await MintAsync(cancellationToken);

        var apiKey = ApiKey.Create(
            tenantId,
            name,
            minted.Prefix,
            ApiKeySecrets.Hash(minted.RawKey),
            expiresAt,
            _protector.Protect(minted.RawKey));

        await _repository.AddAsync(apiKey, cancellationToken);

        return new IssuedApiKey(apiKey, minted.RawKey);
    }

    /// <summary>
    /// Issues a new secret for a key that already exists. The previous secret stops working as
    /// soon as this is saved, because the hash authentication looks up is replaced with the
    /// new one.
    /// </summary>
    public async Task<IssuedApiKey> RotateAsync(
        ApiKey apiKey,
        CancellationToken cancellationToken = default)
    {
        var minted = await MintAsync(cancellationToken);

        apiKey.ReplaceSecret(
            minted.Prefix,
            ApiKeySecrets.Hash(minted.RawKey),
            _protector.Protect(minted.RawKey));

        await _repository.UpdateAsync(apiKey, cancellationToken);

        return new IssuedApiKey(apiKey, minted.RawKey);
    }

    /// <summary>
    /// Generates secrets until one lands on a prefix no other key holds. The prefix is unique
    /// across the whole table, so the check ignores the tenant filter and soft-deletes alike.
    /// </summary>
    private async Task<(string RawKey, string Prefix)> MintAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxPrefixAttempts; attempt++)
        {
            var rawKey = ApiKeySecrets.GenerateRawKey();
            var prefix = ApiKeySecrets.PrefixOf(rawKey);

            if (await _repository.KeyPrefixExistsAsync(prefix, cancellationToken))
            {
                continue;
            }

            return (rawKey, prefix);
        }

        throw new ConflictException(ExceptionMessages.ApiKeyPrefixBusy);
    }
}
