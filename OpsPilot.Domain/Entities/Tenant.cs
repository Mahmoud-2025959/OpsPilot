using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// A company. The one entity that is deliberately not tenant-scoped, because it defines
/// the scoping itself.
/// </summary>
/// <remarks>
/// <para>
/// Note the absence of collection navigations to users, teams, services, API keys, SLA
/// policies and audit logs. That is intentional, and it is the reason this type is small.
/// </para>
/// <para>
/// Those entities are independent aggregates that happen to be scoped by a tenant, not
/// children of a tenant. Scoping is enforced by the global query filter in
/// <c>AppDbContext</c> and by the non-nullable <c>TenantId</c> foreign key on each of them.
/// An aggregate root that owned them would mean loading a whole company — every user,
/// service and audit row — into memory to answer any question about one, which is exactly
/// the kind of accidental coupling that makes large tenants unusable.
/// </para>
/// <para>
/// The collections used to exist here as always-empty read-only lists. Beyond being dead,
/// they were actively harmful: EF's conventions discovered them as navigations and inferred
/// a second relationship on the same <c>TenantId</c> property, which EF resolved by adding a
/// spurious <c>TenantId1</c> column and a duplicate foreign key to six tables.
/// </para>
/// </remarks>
public sealed class Tenant : BaseEntity
{
    private Tenant(string name, string slug)
    {
        Name = name;
        Slug = slug;
    }

    public string Name { get; private set; }

    /// <summary>Stable, URL-safe public handle for the company.</summary>
    public string Slug { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static Tenant Create(string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new Tenant(name.Trim(), slug.Trim().ToLowerInvariant());
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
