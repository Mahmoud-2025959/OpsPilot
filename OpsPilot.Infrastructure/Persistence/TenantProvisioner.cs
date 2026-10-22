using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;
using OpsPilot.Domain.Identity;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Creates a company and the rows every company needs: the global permission catalogue, the
/// five system roles with their permissions, and the default SLA policies (ERD §3).
/// </summary>
/// <remarks>
/// The permission catalogue is global, so it is upserted first and shared by every tenant.
/// Roles and SLA policies are tenant-scoped and are written in one save together with the
/// tenant, so a half-provisioned company can never be observed. The tenant is persisted
/// before its roles because the foreign keys are not deferrable.
/// </remarks>
public sealed class TenantProvisioner : ITenantProvisioner
{
    private readonly AppDbContext _context;
    private readonly ILogger<TenantProvisioner> _logger;

    public TenantProvisioner(AppDbContext context, ILogger<TenantProvisioner> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsurePermissionCatalogAsync(CancellationToken cancellationToken = default)
    {
        // Permission is global and softly filtered only, so this read is tenant-independent.
        var existing = await _context.Permissions
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        var known = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;

        foreach (var definition in PermissionCatalog.All)
        {
            if (known.Contains(definition.Name))
            {
                continue;
            }

            _context.Permissions.Add(Permission.Create(definition.Name, definition.Category, definition.Description));
            added++;
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} permissions into the global catalogue.", added);
        }
    }

    public async Task<ProvisionedTenant> ProvisionAsync(
        string companyName,
        string? slug,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionCatalogAsync(cancellationToken);

        var resolvedSlug = await ResolveSlugAsync(slug, companyName, cancellationToken);

        var tenant = Tenant.Create(companyName, resolvedSlug);
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync(cancellationToken);

        var permissionsByName = await _context.Permissions
            .ToDictionaryAsync(p => p.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var roles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in SystemRoleNames.All)
        {
            var permissions = RolePermissionDefaults.For(roleName)
                .Where(permissionsByName.ContainsKey)
                .Select(name => permissionsByName[name]);

            var role = Role.CreateSystemRole(tenant.Id, roleName, description: null, permissions: permissions);

            _context.Roles.Add(role);
            roles[roleName] = role;
        }

        foreach (var sla in DefaultSlaPolicies.All)
        {
            _context.SlaPolicies.Add(
                SlaPolicy.Create(tenant.Id, sla.Severity, sla.ResponseMinutes, sla.ResolutionMinutes));
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Provisioned tenant {TenantId} ({Slug}) with {RoleCount} roles and {SlaCount} SLA policies.",
            tenant.Id, resolvedSlug, roles.Count, DefaultSlaPolicies.All.Count);

        return new ProvisionedTenant(tenant, roles[SystemRoleNames.Owner]);
    }

    private async Task<string> ResolveSlugAsync(string? slug, string companyName, CancellationToken cancellationToken)
    {
        var supplied = !string.IsNullOrWhiteSpace(slug);

        // The helper truncates to the column width, so an over-long company name is stored as a
        // shortened handle instead of failing the insert.
        var baseSlug = Slug.FromName(supplied ? slug : companyName, "company");

        if (supplied)
        {
            // An explicit handle is a deliberate choice, so a collision is a conflict rather
            // than something to paper over with a suffix.
            if (await _context.Tenants.AnyAsync(t => t.Slug == baseSlug, cancellationToken))
            {
                throw new ConflictException(ExceptionMessages.CompanySlugAlreadyExists);
            }

            return baseSlug;
        }

        var candidate = baseSlug;
        var suffix = 2;

        // The suffix has to fit inside the column too, so the stem is shortened first.
        var stem = baseSlug.Length > Slug.MaxLength - 4
            ? baseSlug[..(Slug.MaxLength - 4)]
            : baseSlug;

        while (await _context.Tenants.AnyAsync(t => t.Slug == candidate, cancellationToken))
        {
            candidate = $"{stem}-{suffix++}";
        }

        return candidate;
    }
}
