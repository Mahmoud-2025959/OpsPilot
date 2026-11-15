using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Identity;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Brings an existing installation up to the permission catalogue this build defines.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TenantProvisioner.EnsurePermissionCatalogAsync"/> runs only when a company is
/// created, so a permission added to the build afterwards would sit in the catalogue and be
/// held by nobody in every company that already exists. This is the other half: it runs at
/// startup, adds the missing catalogue rows, and grants each system role the defaults it is
/// entitled to.
/// </para>
/// <para>
/// Additive only. A permission is granted, never revoked, because a role that was widened or
/// narrowed on purpose must not be reverted by a deployment. Custom roles are left alone
/// entirely: there is no default for them to be brought up to.
/// </para>
/// <para>
/// Each company is visited in its own scope with the ambient tenant entered first. That is not
/// ceremony: the tenant predicate is baked into the EF model the first time a context is built,
/// so a context resolved before <see cref="ICurrentTenant.Enter"/> would carry the unresolved
/// filter and see nothing at all.
/// </para>
/// </remarks>
public sealed class PermissionCatalogSynchronizer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PermissionCatalogSynchronizer> _logger;

    public PermissionCatalogSynchronizer(
        IServiceScopeFactory scopeFactory,
        ILogger<PermissionCatalogSynchronizer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SynchronizeAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host is shutting down before the pass finished. Nothing is half-applied:
            // each company's grants are saved as one unit of work.
        }
        catch (Exception exception)
        {
            // A catalogue that could not be synchronized must not take the application down
            // with it. What is already seeded keeps working, and the next start tries again.
            _logger.LogError(exception, "The permission catalogue could not be synchronized at startup.");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Adds any permission this build defines but the global catalogue is missing, then grants
    /// every system role the defaults it is missing. Returns the number of grants added.
    /// </summary>
    public async Task<int> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        // The catalogue is global, so it is upserted on its own, tenant-independent scope.
        using (var scope = _scopeFactory.CreateScope())
        {
            var provisioner = scope.ServiceProvider.GetRequiredService<ITenantProvisioner>();
            await provisioner.EnsurePermissionCatalogAsync(cancellationToken);
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var directory = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();
            var tenantIds = await directory.GetActiveIdsAsync(cancellationToken);

            var granted = 0;

            foreach (var tenantId in tenantIds)
            {
                granted += await SynchronizeTenantAsync(tenantId, cancellationToken);
            }

            if (granted > 0)
            {
                _logger.LogInformation(
                    "Granted {Count} catalogue permissions across {TenantCount} companies.",
                    granted,
                    tenantIds.Count);
            }

            return granted;
        }
    }

    private async Task<int> SynchronizeTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        // Entered before anything is resolved from the scope: the tenant predicate is baked
        // into the model when the context is first used.
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        using var entered = currentTenant.Enter(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var catalogue = await context.Permissions
            .ToDictionaryAsync(p => p.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var roles = await context.Roles
            .Include(role => role.Permissions)
            .Where(role => role.IsSystem)
            .ToListAsync(cancellationToken);

        var granted = 0;

        foreach (var role in roles)
        {
            var roleName = role.Name ?? string.Empty;

            foreach (var name in RolePermissionDefaults.For(roleName))
            {
                if (!catalogue.TryGetValue(name, out var permission) || role.HasPermission(name))
                {
                    continue;
                }

                role.ApplyCataloguePermission(permission);
                granted++;
            }
        }

        if (granted > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return granted;
    }
}
