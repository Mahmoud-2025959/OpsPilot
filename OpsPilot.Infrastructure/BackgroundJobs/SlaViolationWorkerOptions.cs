namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Settings for the SLA breach sweep (plan, UC-12).
/// </summary>
public sealed class SlaViolationWorkerOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "SlaViolationWorker";

    /// <summary>How often a sweep runs. The plan asks for once a minute.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// False keeps the sweep out of the host altogether. Test hosts set it so that a background
    /// writer never adds rows to the database a test is making assertions about; the production
    /// default is on, because a breach nobody notices is the thing this job exists to prevent.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
