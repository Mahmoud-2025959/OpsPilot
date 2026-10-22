namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Settings for the outbox dispatcher (plan, UC-06 step 7).
/// </summary>
public sealed class OutboxProcessorOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "OutboxProcessor";

    /// <summary>
    /// False keeps the dispatcher out of the host altogether. Test hosts set it so that a
    /// background writer never touches the database a test is making assertions about; the
    /// production default is on, because an undrained outbox is silent data loss.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often a drain runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How many messages a single pass takes.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>How many times one message is tried before it is parked as failed.</summary>
    public int MaxAttempts { get; set; } = 5;
}
