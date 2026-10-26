using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RoleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Tenant scoping is deliberately absent: the global query filter has already narrowed
        // this set to the ambient tenant.
        return await _context.Roles
            .Include(r => r.Permissions)
            .AsSplitQuery()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // The permission set is mutated through the loaded collection, so it has to come along.
        return _context.Roles
            .Include(r => r.Permissions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(
        string name,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        // The database compares the normalized name, so the check has to as well -- otherwise
        // "Engineer" and "engineer" would both pass and one of them would fail on insert.
        var normalized = name.Trim().ToUpperInvariant();

        return _context.Roles.AnyAsync(
            r => r.NormalizedName == normalized && (excludingId == null || r.Id != excludingId),
            cancellationToken);
    }

    public async Task<Guid> AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        await _context.Roles.AddAsync(role, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return role.Id;
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        _context.Roles.Update(role);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasUsersAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        // Asked through the Users set on purpose: its global filters drop deleted users, and the
        // Role filter drops other companies, so this answers "does anyone in this company still
        // hold it?" rather than "do any rows point at it?".
        return _context.Users.AnyAsync(
            u => u.Roles.Any(r => r.Id == roleId),
            cancellationToken);
    }

    public async Task DeleteAsync(Role role, CancellationToken cancellationToken = default)
    {
        role.SoftDelete();
        _context.Roles.Update(role);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
