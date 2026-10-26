namespace OpsPilot.Domain.Exceptions;

/// <summary>
/// A webhook call that could not be made, or that the endpoint refused.
/// </summary>
/// <remarks>
/// The distinction the dispatcher cares about is between a refusal that will keep happening
/// and a failure that might not: a 4xx means the endpoint will answer the same way next time,
/// a 5xx or a dropped connection might not. The flag carries that judgement, so the retry
/// rules live in one place instead of being re-derived at every call site.
/// </remarks>
public sealed class WebhookDeliveryException : Exception
{
    public WebhookDeliveryException(string message, bool isPermanent = false)
        : base(message)
    {
        IsPermanent = isPermanent;
    }

    /// <summary>True when trying again would fail the same way.</summary>
    public bool IsPermanent { get; }
}
