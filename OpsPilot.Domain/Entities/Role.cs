using Microsoft.AspNetCore.Identity;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// The five roles provisioned for every new tenant (ERD §3, "Identity و Tenancy").
/// </summary>
public static class SystemRoleNames
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string TeamLead = "TeamLead";
    public const string Engineer = "Engineer";
    public const string Viewer = "Viewer";

    /// <summary>All default roles, in descending order of privilege.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Owner, Admin, TeamLead, Engineer, Viewer];
}

/// <summary>
/// A tenant-scoped set of permissions.
/// </summary>
/// <remarks>
/// Extends <see cref="IdentityRole{TKey}"/> so ASP.NET Identity can manage it, which is
/// why the audit members are declared here rather than inherited from
/// <see cref="BaseEntity"/>. The domain rules from ERD §3 are enforced on this type:
/// system roles cannot be renamed or deleted, and the Owner role's permissions are frozen.
/// </remarks>
public sealed class Role : IdentityRole<Guid>, IAuditableEntity, ISoftDeletable, ITenantOwned
{
    private readonly List<Permission> _permissions = [];

    public Role()
    {
        Id = Guid.NewGuid();
        ConcurrencyStamp = Guid.NewGuid().ToString();
    }

    private Role(string name, string? description) : this()
    {
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        Description = description;
    }

    public Guid TenantId { get; private set; }

    public string? Description { get; private set; }

    /// <summary>
    /// True for the five roles seeded at tenant creation. System roles are protected from
    /// renaming and deletion.
    /// </summary>
    public bool IsSystem { get; private set; }

    public bool IsDeleted { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public IReadOnlyCollection<Permission> Permissions => _permissions;

    /// <summary>True when this is the tenant's Owner role, whose permissions are frozen.</summary>
    public bool IsOwner => string.Equals(Name, SystemRoleNames.Owner, StringComparison.OrdinalIgnoreCase);

    /// <summary>Creates a custom (non-system) role.</summary>
    public static Role Create(Guid tenantId, string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role(name.Trim(), description?.Trim())
        {
            TenantId = tenantId
        };
    }

    /// <summary>Creates one of the five protected roles provisioned with a tenant.</summary>
    /// <param name="tenantId">The company the role belongs to.</param>
    /// <param name="name">The role name, one of <see cref="SystemRoleNames"/>.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="permissions">
    /// The permissions seeded with the role. This is the one place the Owner role may receive
    /// permissions: <see cref="AssignPermission"/> is deliberately closed afterwards, because
    /// the Owner's set is fixed for the lifetime of the tenant.
    /// </param>
    public static Role CreateSystemRole(
        Guid tenantId,
        string name,
        string? description = null,
        IEnumerable<Permission>? permissions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var role = new Role(name.Trim(), description?.Trim())
        {
            TenantId = tenantId,
            IsSystem = true
        };

        if (permissions is not null)
        {
            foreach (var permission in permissions)
            {
                if (permission is not null && !role._permissions.Contains(permission))
                {
                    role._permissions.Add(permission);
                }
            }
        }

        return role;
    }

    public void Rename(string name, string? description = null)
    {
        EnsureMutable("renamed");

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        NormalizedName = name.Trim().ToUpperInvariant();
        Description = description?.Trim();
    }

    public void AssignPermission(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        EnsurePermissionsMutable();

        if (!_permissions.Contains(permission))
        {
            _permissions.Add(permission);
        }
    }

    public bool RemovePermission(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        EnsurePermissionsMutable();

        return _permissions.Remove(permission);
    }

    public bool HasPermission(string permissionName) => _permissions.Any(p => p.Name == permissionName);

    public void SoftDelete()
    {
        if (IsSystem)
        {
            throw new OperationNotPermittedException($"System role '{Name}' cannot be deleted.");
        }

        IsDeleted = true;
    }

    public void Restore()
    {
        if (IsSystem)
        {
            throw new OperationNotPermittedException($"System role '{Name}' cannot be deleted.");
        }

        IsDeleted = false;
    }

    private void EnsureMutable(string operation)
    {
        if (IsSystem)
        {
            throw new OperationNotPermittedException($"System role '{Name}' cannot be {operation}.");
        }
    }

    private void EnsurePermissionsMutable()
    {
        if (IsOwner)
        {
            throw new OperationNotPermittedException(
                "The Owner role's permissions are fixed and cannot be modified.");
        }
    }
}
