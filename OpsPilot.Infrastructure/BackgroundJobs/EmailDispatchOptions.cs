namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>Settings for the sweep that delivers queued notification emails.</summary>
public sealed class EmailDispatchOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "EmailDispatch";

    /// <summary>
    /// False keeps the sweep out of the host altogether. Test hosts set it so a background
    /// writer never touches the database a test is making assertions about; the production
    /// default is on, because undelivered notifications are silent data loss.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often a pass runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How many queued emails one company's pass takes.</summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>
    /// How many times one email is tried before it is parked as failed. Bounded on purpose:
    /// a transport outage must not leave an unbounded queue of retries behind it.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;
}
