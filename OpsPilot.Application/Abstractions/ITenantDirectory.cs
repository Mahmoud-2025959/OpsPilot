namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Lists the companies a background sweep has to visit (plan, UC-12).
/// </summary>
/// <remarks>
/// The one read in the system that is deliberately cross-tenant: <c>Tenant</c> is what defines
/// scoping, so it is not itself scoped. Every job that has to work for every company starts
/// here and then enters one company at a time — see <see cref="ICurrentTenant.Enter"/> — rather
/// than trying to read everybody's rows at once.
/// </remarks>
public interface ITenantDirectory
{
    /// <summary>Ids of every company that is still in use.</summary>
    Task<IReadOnlyList<Guid>> GetActiveIdsAsync(CancellationToken cancellationToken = default);
}
