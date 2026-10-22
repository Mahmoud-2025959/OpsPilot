namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Settings for the Hangfire scheduler (plan, "BackgroundJobs" and the SLA/Hangfire line).
/// </summary>
/// <remarks>
/// <para>
/// Hangfire is introduced as the scheduler, not as the work. The rules — which incidents count as
/// late, how a message is retried, what makes an email permanent-failed — stay in the application
/// handlers and in the workers' pass methods; Hangfire only decides <em>when</em> a pass runs and
/// gives the passes a dashboard, retries and a distributed lock so two instances do not sweep the
/// same estate at the same time.
/// </para>
/// <para>
/// When <see cref="Enabled"/> is false the workers fall back to their own <c>PeriodicTimer</c>
/// loop, which is what they have always done. That keeps the test host and a developer without a
/// Hangfire schema on the same behaviour as before this was added.
/// </para>
/// </remarks>
public sealed class HangfireOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "Hangfire";

    /// <summary>
    /// Whether Hangfire owns the schedules. When on, the workers' own timers do not start; the
    /// recurring jobs below call the same pass methods instead.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Whether to publish the Hangfire dashboard. The dashboard shows every job, its history and
    /// the ability to re-run one by hand, which is most of the reason to adopt Hangfire — but it
    /// is also an unauthenticated admin surface, so it is a separate switch a deployment can keep
    /// off.
    /// </summary>
    public bool DashboardEnabled { get; set; } = true;

    /// <summary>
    /// Where the dashboard is mounted. Deliberately not under <c>/api</c> so it never appears in
    /// the versioned OpenAPI document.
    /// </summary>
    public string DashboardPath { get; set; } = "/jobs";

    /// <summary>Cron for the SLA breach sweep. Default: every minute, matching the worker.</summary>
    public string SlaSweepCron { get; set; } = "* * * * *";

    /// <summary>Cron for the outbox drain. Default: every thirty seconds.</summary>
    public string OutboxDrainCron { get; set; } = "*/30 * * * * *";

    /// <summary>Cron for the email dispatch. Default: every thirty seconds.</summary>
    public string EmailDispatchCron { get; set; } = "*/30 * * * * *";

    /// <summary>How many times Hangfire itself re-runs a job whose body threw.</summary>
    public int MaxAutomaticRetries { get; set; } = 3;
}
