namespace OpsPilot.Domain.Enums;

/// <summary>
/// Delivery state of a webhook call.
/// </summary>
/// <remarks>
/// The same three states a queued notification has: waiting, done, or given up on. The
/// difference is what "given up on" means — a webhook that keeps failing is parked rather
/// than retried forever, because the endpoint on the other end is not going to start
/// answering between one pass and the next.
/// </remarks>
public enum WebhookDeliveryStatus
{
    Pending,
    Delivered,
    Failed
}
