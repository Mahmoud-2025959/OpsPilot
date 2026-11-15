using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class SlaPolicyRepository : ISlaPolicyRepository
{
    private readonly AppDbContext _context;

    public SlaPolicyRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SlaPolicy>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Severity is persisted as a string, so an OrderBy on the IQueryable would be translated
        // as an alphabetical sort on that column (Critical, High, Low, Medium) rather than by
        // severity. There are only four rows here, so sorting in memory by the enum's numeric
        // value restores the ranking the settings screen shows and the plan assumes.
        var policies = await _context.SlaPolicies.ToListAsync(cancellationToken);

        return policies.OrderBy(p => p.Severity).ToList();
    }

    public Task<SlaPolicy?> GetBySeverityAsync(Severity severity, CancellationToken cancellationToken = default)
    {
        return _context.SlaPolicies
            .FirstOrDefaultAsync(p => p.Severity == severity, cancellationToken);
    }

    public async Task UpdateAsync(SlaPolicy policy, CancellationToken cancellationToken = default)
    {
        _context.SlaPolicies.Update(policy);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(
        IReadOnlyList<SlaPolicy> policies,
        CancellationToken cancellationToken = default)
    {
        if (policies.Count == 0)
        {
            return;
        }

        _context.SlaPolicies.UpdateRange(policies);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
