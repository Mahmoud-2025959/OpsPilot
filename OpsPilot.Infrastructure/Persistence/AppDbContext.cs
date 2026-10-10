using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Entities;
using OpsPilot.Infrastructure.Persistence.Interceptors;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<User, Role, Guid>
{
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// The tenant the model cache key for this context is derived from — see
    /// <see cref="Persistence.TenantModelCacheKeyFactory"/>.
    /// </summary>
    internal Guid CurrentTenantId => _currentTenant.TenantId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<IncidentComment> IncidentComments => Set<IncidentComment>();

    public DbSet<IncidentEvent> IncidentEvents => Set<IncidentEvent>();

    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();

    public DbSet<SlaViolation> SlaViolations => Set<SlaViolation>();

    public DbSet<Postmortem> Postmortems => Set<Postmortem>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();

    public DbSet<AiSuggestion> AiSuggestions => Set<AiSuggestion>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        MapIdentityTables(modelBuilder);

        ApplyGlobalQueryFilters(modelBuilder);
    }

    private static void MapIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("UserClaims"));
        modelBuilder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("UserLogins"));
        modelBuilder.Entity<IdentityRoleClaim<Guid>>(b => b.ToTable("RoleClaims"));
        modelBuilder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("UserTokens"));
    }

    /// <summary>
    /// Installs exactly one query filter per entity, composing the soft-delete predicate
    /// and the tenant predicate where both apply.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Centralising this here is what makes the two rules in ERD §1 impossible to get
    /// half-applied. Individual entity configurations are free to describe keys, indexes
    /// and delete behaviours without each having to remember that tenant scoping exists.
    /// </para>
    /// <para>
    /// The tenant side bakes the ambient tenant id into the predicate as a plain value.
    /// Capturing the <see cref="ICurrentTenant"/> <em>instance</em> instead would survive only
    /// by accident: EF caches one model per cache key and reuses it for every context that
    /// maps to that key, so the filter would keep reading whichever scope happened to build
    /// the model first. Pairing the baked value with
    /// <see cref="TenantModelCacheKeyFactory"/> — which folds the same id into the cache key —
    /// guarantees a model is only ever served to a context whose tenant is that same id, and
    /// the two can never disagree.
    /// </para>
    /// <para>
    /// When no tenant is resolved the predicate compares against <see cref="Guid.Empty"/> and
    /// therefore matches nothing, so an unscoped request fails closed instead of leaking every
    /// tenant's rows. The corollary is that the ambient tenant must be established before the
    /// first query on a context; switching tenants on an already-used context is not supported.
    /// </para>
    /// </remarks>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        var tenantId = _currentTenant.TenantId;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var predicate = BuildQueryFilter(entityType.ClrType, tenantId);

            if (predicate is not null)
            {
                entityType.SetQueryFilter(predicate);
            }
        }
    }

    private static LambdaExpression? BuildQueryFilter(Type clrType, Guid tenantId)
    {
        var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);
        var isTenantOwned = typeof(ITenantOwned).IsAssignableFrom(clrType);

        if (!isSoftDeletable && !isTenantOwned)
        {
            return null;
        }

        var parameter = Expression.Parameter(clrType, "e");
        Expression? predicate = null;

        if (isSoftDeletable)
        {
            predicate = Expression.Equal(
                Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)),
                Expression.Constant(false));
        }

        if (isTenantOwned)
        {
            var tenantPredicate = Expression.Equal(
                Expression.Property(parameter, nameof(ITenantOwned.TenantId)),
                Expression.Constant(tenantId, typeof(Guid)));

            predicate = predicate is null ? tenantPredicate : Expression.AndAlso(predicate, tenantPredicate);
        }

        return Expression.Lambda(predicate!, parameter);
    }
}
