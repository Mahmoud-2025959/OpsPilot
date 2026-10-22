using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically marks a token used and links it to its replacement, but only while it is still
    /// unused and unrevoked. Returns <c>false</c> when another caller got there first.
    /// </summary>
    /// <remarks>
    /// Rotation is single-use, and the single-use guarantee has to be decided by the database:
    /// reading the token and then saving it as used is a check-then-act that two simultaneous
    /// refreshes both pass.
    /// </remarks>
    Task<bool> TryConsumeAsync(
        Guid tokenId,
        Guid replacementTokenId,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
