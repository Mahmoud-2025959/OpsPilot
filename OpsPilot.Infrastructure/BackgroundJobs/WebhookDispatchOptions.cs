namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>Settings for the sweep that delivers queued webhook calls.</summary>
public sealed class WebhookDispatchOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "WebhookDispatch";

    /// <summary>
    /// False keeps the sweep out of the host altogether. The production default is on,
    /// because a webhook that is never called is a silent loss of whatever it was
    /// registered to hear about.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often a pass runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>How many queued calls one company's pass takes.</summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>
    /// How many times one call is tried before it is parked as failed. Bounded on purpose: an
    /// endpoint that is down must not leave an unbounded queue of retries behind it.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>How long a single call may take before it is given up on.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
