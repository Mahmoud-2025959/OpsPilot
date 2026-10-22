using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens.AddAsync(token, cancellationToken);
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        // Refresh happens before a bearer exists, so no tenant can be resolved yet. The hash
        // is globally unique (it is a SHA-256 of 64 random bytes), so cross-tenant lookup is
        // safe and is the only way to bootstrap the tenant for the rest of the flow.
        return _context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => !t.IsDeleted && t.TokenHash == tokenHash, cancellationToken);
    }

    public Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        _context.RefreshTokens.Update(token);
        return Task.CompletedTask;
    }

    public async Task<bool> TryConsumeAsync(
        Guid tokenId,
        Guid replacementTokenId,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default)
    {
        // One conditional UPDATE decides the winner inside the database. The WHERE clause makes
        // the check and the write a single atomic step, so of two concurrent refreshes exactly
        // one affects a row; the loser observes zero and is treated as a replay. This is done
        // with ExecuteUpdate rather than load-then-save because the latter would re-read the
        // row and re-introduce the race it is meant to close.
        var affected = await _context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.Id == tokenId && !t.IsUsed && !t.IsRevoked && !t.IsDeleted)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.IsUsed, true)
                    .SetProperty(t => t.ReplacedByTokenId, replacementTokenId)
                    .SetProperty(t => t.UpdatedAt, consumedAt),
                cancellationToken);

        return affected == 1;
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Reuse detection is a security sweep keyed by a globally unique user id; it must see
        // the whole chain even though the caller is still anonymous (no tenant resolved yet).
        var tokens = await _context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted && t.UserId == userId && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(DateTimeOffset.UtcNow);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
