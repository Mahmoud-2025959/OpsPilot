using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class TenantRepository : ITenantRepository
{
    private readonly AppDbContext _context;

    public TenantRepository(AppDbContext context) => _context = context;

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Tenant is the row the tenant filter is built around, so this read is deliberately
        // not filtered by the ambient tenant.
        return _context.Tenants.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _context.Tenants.Update(tenant);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
