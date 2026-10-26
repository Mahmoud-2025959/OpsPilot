using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// The webhook endpoints of a company and the calls waiting to be made to them.
/// </summary>
/// <remarks>
/// The endpoints are tenant-scoped and read through the global query filter, so there is
/// deliberately no tenant parameter on any of these methods. The deliveries are tenant-scoped
/// for the same reason.
/// </remarks>
public interface IWebhookRepository
{
    Task<Guid> AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one endpoint. Tenant-scoped, so an endpoint belonging to another company is a
    /// miss rather than something the caller can read.
    /// </summary>
    Task<WebhookEndpoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default);

    /// <summary>Retires an endpoint. The row stays, so the calls already made still make sense.</summary>
    Task RemoveAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WebhookEndpoint> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another endpoint already claims <paramref name="url"/> in this company. The
    /// database enforces the same rule with a unique index on <c>(TenantId, Url)</c>.
    /// </summary>
    Task<bool> UrlExistsAsync(
        string url,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The calls waiting to be made, each with the endpoint it is addressed to.
    /// </summary>
    Task<IReadOnlyList<(WebhookDelivery Delivery, WebhookEndpoint Endpoint)>> GetPendingDeliveriesAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>Saves what became of the calls made in this pass.</summary>
    Task SaveDeliveriesAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WebhookDelivery> Items, int TotalCount)> GetDeliveriesPagedAsync(
        Guid? endpointId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
