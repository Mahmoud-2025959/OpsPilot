using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class ApiKeyRepository : IApiKeyRepository
{
    private readonly AppDbContext _context;

    public ApiKeyRepository(AppDbContext context) => _context = context;

    public async Task<Guid> AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync(cancellationToken);
        return apiKey.Id;
    }

    public Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        _context.ApiKeys.Update(apiKey);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ApiKey> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Tenant scoping is deliberately absent: the global query filter has already narrowed
        // this set to the ambient tenant.
        var query = _context.ApiKeys.AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(k => k.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<bool> NameExistsAsync(
        string name,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();

        return _context.ApiKeys.AnyAsync(
            k => k.Name == trimmed && (excludingId == null || k.Id != excludingId),
            cancellationToken);
    }

    public Task<bool> KeyPrefixExistsAsync(string keyPrefix, CancellationToken cancellationToken = default)
    {
        // The unique index over KeyPrefix spans the whole table and ignores soft-deletes, so the
        // check has to match it: filters are bypassed on purpose.
        return _context.ApiKeys
            .IgnoreQueryFilters()
            .AnyAsync(k => k.KeyPrefix == keyPrefix, cancellationToken);
    }

    public Task<ApiKey?> FindByKeyHashAcrossTenantsAsync(
        string keyHash,
        CancellationToken cancellationToken = default)
    {
        // Authentication runs before any tenant is known, so this crosses tenants on purpose --
        // the hash identifies exactly one key, and the tenant is read back from it afterwards
        // (the same reasoning as the login-by-email reads). Soft-deleted keys are still excluded.
        return _context.ApiKeys
            .IgnoreQueryFilters()
            .Where(k => !k.IsDeleted)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);
    }
}
