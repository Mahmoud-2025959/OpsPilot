namespace OpsPilot.Contracts.Webhooks;

/// <summary>
/// A webhook endpoint as it appears in the list.
/// </summary>
/// <remarks>
/// <see cref="Secret"/> is carried out in the clear because the list is the view an operator
/// uses to recover the signing secret they configured on the other end — the same trade the
/// ingestion keys make. The endpoint that produces this needs the <c>webhooks.manage</c>
/// permission, so it hands out no more than the people who may register endpoints already
/// have.
/// <para>
/// <see cref="Secret"/> is null for an endpoint registered before the secret began being
/// stored: a hash cannot be reversed into the secret it was made from, so those rows have to
/// be rotated before they have a usable value.
/// </para>
/// </remarks>
public sealed record WebhookEndpointDto(
    Guid Id,
    Guid TenantId,
    string Url,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? Secret = null);

/// <summary>
/// The one and only time a plain signing secret is returned. It is also returned when the
/// secret is rotated, which is the way to recover the value of an endpoint that predates the
/// stored copy.
/// </summary>
public sealed record CreatedWebhookDto(
    Guid Id,
    string Url,
    string Secret,
    DateTimeOffset CreatedAt);

/// <summary>
/// One webhook call and what became of it.
/// </summary>
public sealed record WebhookDeliveryDto(
    Guid Id,
    Guid EndpointId,
    string EventType,
    string Status,
    int Attempts,
    string? LastError,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset CreatedAt);
