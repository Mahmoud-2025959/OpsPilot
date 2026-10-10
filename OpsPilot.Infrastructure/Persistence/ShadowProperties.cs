namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Names of EF shadow properties that exist only in the persistence model.
/// </summary>
/// <remarks>
/// Shadow properties keep concerns that only matter to storage — most importantly the
/// tenant discriminator on the ASP.NET Identity join tables — out of the domain model,
/// while still giving the interceptors and configurations one name to agree on.
/// </remarks>
public static class ShadowProperties
{
    public const string TenantId = "TenantId";

    public const string RowVersion = "RowVersion";
}
