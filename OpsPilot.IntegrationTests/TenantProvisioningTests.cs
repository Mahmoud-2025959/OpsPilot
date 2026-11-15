using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Identity;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers tenant provisioning: the step that turns an empty database into one a person can
/// actually sign in to. It is the difference between "registration returns 401" and a working
/// company, so the roles, permissions and SLA policies it writes are asserted for real.
/// </summary>
public sealed class TenantProvisioningTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task ProvisionAsync_CreatesTenantWithSystemRolesAndSlaPolicies()
    {
        var tenants = new TenantContext();
        ProvisionedTenant provisioned;

        // Writes do not need a tenant; the real id only exists after the tenant is saved.
        await using (var writeContext = fixture.CreateContext(tenants))
        {
            var provisioner = new TenantProvisioner(writeContext, NullLogger<TenantProvisioner>.Instance);
            provisioned = await provisioner.ProvisionAsync("Globex", slug: null);
        }

        Assert.NotEqual(Guid.Empty, provisioned.Tenant.Id);
        Assert.True(provisioned.OwnerRole.IsOwner);

        using var scope = tenants.Enter(provisioned.Tenant.Id);
        await using var read = fixture.CreateContext(tenants);

        var roles = await read.Roles.Include(r => r.Permissions).ToListAsync();

        Assert.Equal(SystemRoleNames.All.Count, roles.Count);
        Assert.All(roles, role => Assert.True(role.IsSystem));

        var slaPolicies = await read.SlaPolicies.ToListAsync();

        Assert.Equal(DefaultSlaPolicies.All.Count, slaPolicies.Count);
        Assert.Equal(
            DefaultSlaPolicies.All.Select(p => p.Severity).OrderBy(s => s),
            slaPolicies.Select(p => p.Severity).OrderBy(s => s));
    }

    [Fact]
    public async Task ProvisionAsync_GrantsOwnerEveryHumanPermission()
    {
        var tenants = new TenantContext();
        ProvisionedTenant provisioned;

        await using (var writeContext = fixture.CreateContext(tenants))
        {
            var provisioner = new TenantProvisioner(writeContext, NullLogger<TenantProvisioner>.Instance);
            provisioned = await provisioner.ProvisionAsync("Initech", slug: null);
        }

        using var scope = tenants.Enter(provisioned.Tenant.Id);
        await using var read = fixture.CreateContext(tenants);

        var owner = await read.Roles
            .Include(r => r.Permissions)
            .SingleAsync(r => r.Name == SystemRoleNames.Owner);

        var actual = owner.Permissions.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var expected = PermissionCatalog.Names
            .Where(n => n != PermissionNames.EventsIngest)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ProvisionAsync_SeedsPermissionCatalogueOnlyOnce()
    {
        var tenants = new TenantContext();

        // Two companies must share one catalogue, not grow it.
        await using (var writeContext = fixture.CreateContext(tenants))
        {
            var provisioner = new TenantProvisioner(writeContext, NullLogger<TenantProvisioner>.Instance);
            await provisioner.ProvisionAsync("Umbrella", slug: null);
            await provisioner.ProvisionAsync("Wayne", slug: null);
        }

        await using var read = fixture.CreateContext(tenants);
        var permissionCount = await read.Permissions.CountAsync();

        Assert.Equal(PermissionCatalog.All.Count, permissionCount);
    }
}
