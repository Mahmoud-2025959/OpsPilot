using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Extends EF's model cache key with the tenant the model is being built for.
/// </summary>
/// <remarks>
/// <para>
/// EF's default key is the context type alone, which assumes every context of a type shares
/// one model. That assumption breaks the tenant half of the query filter installed by
/// <see cref="AppDbContext"/>: the predicate is baked into the model when it is built, so a
/// model built for tenant A (or, worse, for an unresolved tenant) would be served to every
/// other tenant — silently returning another company's rows, or none at all.
/// </para>
/// <para>
/// Including the tenant id in the key gives each tenant its own model whose baked-in
/// predicate matches its rows, while contexts that share a tenant also share the built
/// model. An unresolved tenant maps to <see cref="Guid.Empty"/>, whose filter matches
/// nothing — the fail-closed behaviour required by ERD section 1.
/// </para>
/// <para>
/// The tenant id must therefore be stable before the first model access on a context;
/// establish the ambient tenant before the first query or save, which is exactly the
/// request-pipeline contract in production and the fixture contract in tests.
/// </para>
/// </remarks>
public sealed class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <summary>Builds the cache key for <paramref name="context"/>.</summary>
    public object Create(DbContext context, bool designTime)
        => context is AppDbContext app
            ? (typeof(AppDbContext), app.CurrentTenantId, designTime)
            : (context.GetType(), designTime);
}
