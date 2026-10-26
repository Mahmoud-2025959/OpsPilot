using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Reads the list of companies from the one table that is not tenant-scoped (plan, UC-12).
/// </summary>
public sealed class TenantDirectory : ITenantDirectory
{
    private readonly AppDbContext _context;

    public TenantDirectory(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Guid>> GetActiveIdsAsync(CancellationToken cancellationToken = default)
    {
        // No IgnoreQueryFilters is needed here, and that is the point: Tenant is what scoping is
        // defined against, so it carries no tenant predicate to opt out of. Companies that have
        // been switched off are skipped because nothing in them is being watched any more.
        return await _context.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.IsActive)
            .Select(tenant => tenant.Id)
            .ToListAsync(cancellationToken);
    }
}
