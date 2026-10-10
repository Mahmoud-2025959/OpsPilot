using Microsoft.AspNetCore.Identity;
using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// A tenant-scoped account, built on ASP.NET Identity.
/// </summary>
/// <remarks>
/// Extends <see cref="IdentityUser{TKey}"/> so Identity can manage it, which is why the
/// audit members are declared here rather than inherited from <see cref="BaseEntity"/>.
/// Per ERD §4 a user is never deleted: it is disabled instead.
/// </remarks>
public sealed class User : IdentityUser<Guid>, IAuditableEntity, ISoftDeletable, ITenantOwned
{
    private readonly List<Role> _roles = [];

    public User()
    {
        Id = Guid.NewGuid();
        SecurityStamp = Guid.NewGuid().ToString();
        ConcurrencyStamp = Guid.NewGuid().ToString();
    }

    private User(string email, string displayName, bool isActive) : this()
    {
        Email = email;
        NormalizedEmail = email.ToUpperInvariant();
        UserName = email;
        NormalizedUserName = email.ToUpperInvariant();
        DisplayName = displayName;
        IsActive = isActive;
    }

    public Guid TenantId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public bool IsDeleted { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public IReadOnlyCollection<Role> Roles => _roles;

    public static User Create(Guid tenantId, string email, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new User(email.Trim(), displayName.Trim(), true)
        {
            TenantId = tenantId
        };
    }

    public void AssignRole(Role role)
    {
        if (role.TenantId != TenantId)
        {
            throw new OpsPilot.Domain.Exceptions.OperationNotPermittedException(
                "Cannot assign a role from a different tenant.");
        }

        if (!_roles.Contains(role))
        {
            _roles.Add(role);
        }
    }

    public bool RemoveRole(Role role) => _roles.Remove(role);

    public bool HasRole(string roleName) => _roles.Any(r => r.Name == roleName);

    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProfile(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var trimmed = email.Trim();
        Email = trimmed;
        NormalizedEmail = trimmed.ToUpperInvariant();
        UserName = trimmed;
        NormalizedUserName = trimmed.ToUpperInvariant();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordLogin(DateTimeOffset at)
    {
        LastLoginAt = at;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Retires the account. Per ERD §4 users are never deleted, so "deleting" only ever
    /// disables the account and preserves every incident it is referenced by.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}