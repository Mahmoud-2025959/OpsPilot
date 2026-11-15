using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Two collection navigations (roles and their permissions) in one query: split, or the
        // cartesian product of roles x permissions comes back in a single round trip.
        return _context.Users
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToUpperInvariant();

        return _context.Users
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                u => u.TenantId == tenantId && u.NormalizedEmail == normalized,
                cancellationToken);
    }

    public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToUpperInvariant();

        return _context.Users.AnyAsync(
            u => u.TenantId == tenantId && u.NormalizedEmail == normalized,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        string? search,
        string? role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .AsSplitQuery()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                EF.Functions.Like(u.Email, $"%{term}%") ||
                EF.Functions.Like(u.DisplayName, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleName = role.Trim();
            query = query.Where(u => u.Roles.Any(r => r.Name == roleName));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        user.SoftDelete();
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<User?> FindByEmailAcrossTenantsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToUpperInvariant();

        // IgnoreQueryFilters drops the soft-delete filter as well, so IsDeleted is re-applied
        // explicitly to keep retired accounts unauthenticatable.
        return _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => !u.IsDeleted && u.NormalizedEmail == normalized, cancellationToken);
    }

    public Task<bool> EmailExistsAcrossTenantsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToUpperInvariant();

        return _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => !u.IsDeleted && u.NormalizedEmail == normalized, cancellationToken);
    }

    public Task<bool> IsActiveAcrossTenantsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters also drops the soft-delete predicate, so it is restated here: a
        // deleted account must fail this check exactly like a deactivated one.
        return _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Id == id && !u.IsDeleted && u.IsActive, cancellationToken);
    }

    public Task<User?> FindByIdAcrossTenantsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // The role grant guard reads the caller's roles and permissions through here, so this
        // query runs on every role change: split it rather than joining two collections.
        return _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => !u.IsDeleted && u.Id == id, cancellationToken);
    }

    public Task<string?> GetDisplayNameAcrossTenantsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Only the name column: this runs once per live message, and joining roles and
        // permissions to render a label would be wasted work. Deleted accounts read as absent,
        // so the caller falls back rather than labelling a message with a ghost.
        return _context.Users
            .IgnoreQueryFilters()
            .Where(u => !u.IsDeleted && u.Id == id)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetRolesByNamesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default)
    {
        var normalized = names.Select(n => n.Trim()).ToList();

        return await _context.Roles
            .Include(r => r.Permissions)
            .Where(r => r.TenantId == tenantId && r.Name != null && normalized.Contains(r.Name))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AnotherActiveOwnerExistsAsync(
        Guid excludedUserId,
        CancellationToken cancellationToken = default)
    {
        // Tenant scoping and the soft-delete filter both come from the global query filter, so
        // only the two rules that are specific to this question are stated here: the account must
        // be usable, and it must not be the one being demoted or disabled.
        return _context.Users.AnyAsync(
            u => u.Id != excludedUserId
                && u.IsActive
                && u.Roles.Any(r => r.Name == SystemRoleNames.Owner),
            cancellationToken);
    }

    public async Task<T> ExecuteWithOwnerGuardAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // The lock is owned by the transaction, so it is released even when the operation
        // throws -- including the conflict this guard exists to raise.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await AcquireOwnerGuardLockAsync(cancellationToken);

        var result = await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task ExecuteWithOwnerGuardAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
        => ExecuteWithOwnerGuardAsync(
            async ct =>
            {
                await operation(ct);
                return true;
            },
            cancellationToken);

    private async Task AcquireOwnerGuardLockAsync(CancellationToken cancellationToken)
    {
        // A transaction-scoped application lock keyed by tenant. LockOwner = 'Transaction' ties
        // its lifetime to the transaction opened above, so an early return cannot leave it held.
        var resource = $"OpsPilot.OwnerGuard:{_context.CurrentTenantId}";

        // sp_getapplock reports an ungranted lock (for example on timeout) by returning a
        // negative status rather than throwing, so the value has to be read and checked.
        var statuses = await _context.Database
            .SqlQueryRaw<int>(
                """
                DECLARE @status int;
                EXEC @status = sp_getapplock
                    @Resource = {0},
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;
                SELECT @status AS Value;
                """,
                resource)
            .ToListAsync(cancellationToken);

        if (statuses.Count != 1 || statuses[0] < 0)
        {
            throw new ConflictException(ExceptionMessages.OwnerChangesBusy);
        }
    }
}
