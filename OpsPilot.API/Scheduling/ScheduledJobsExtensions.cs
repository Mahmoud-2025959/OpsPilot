using Hangfire;
using Hangfire.Dashboard;
using Microsoft.Extensions.Options;
using OpsPilot.Infrastructure.BackgroundJobs;

namespace OpsPilot.API.Scheduling;

/// <summary>
/// Registers the recurring passes and, optionally, the Hangfire dashboard (plan,
/// "BackgroundJobs").
/// </summary>
/// <remarks>
/// <para>
/// Registration has to happen after the container is built — a recurring job is written to the
/// Hangfire storage, which means it needs the job manager, not just a service registration. That
/// is why this is separate from <c>AddScheduledJobs</c>, which only sets the storage and server
/// up.
/// </para>
/// <para>
/// Each job is registered with a stable id (<c>RecurringJob.AddOrUpdate</c> is an upsert keyed by
/// that id), so a changed cron in configuration takes effect on the next deploy rather than
/// stacking a second job beside the old one.
/// </para>
/// </remarks>
public static class ScheduledJobsExtensions
{
    /// <summary>Stable id for the SLA breach sweep.</summary>
    public const string SlaSweepJobId = "sla-violation-sweep";

    /// <summary>Stable id for the outbox drain.</summary>
    public const string OutboxDrainJobId = "outbox-drain";

    /// <summary>Stable id for the email dispatch.</summary>
    public const string EmailDispatchJobId = "email-dispatch";

    /// <summary>Stable id for the webhook dispatch.</summary>
    public const string WebhookDispatchJobId = "webhook-dispatch";

    /// <summary>Registers the four recurring jobs and mounts the dashboard.</summary>
    public static WebApplication UseScheduledJobs(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<HangfireOptions>>().Value;

        if (!options.Enabled)
        {
            return app;
        }

        var jobs = app.Services.GetRequiredService<IRecurringJobManager>();

        // The job body is a method on ScheduledJobs, so Hangfire resolves it from the container
        // per run and the actual work stays in the workers it delegates to.
        jobs.AddOrUpdate<ScheduledJobs>(
            SlaSweepJobId,
            job => job.RunSlaSweepAsync(),
            options.SlaSweepCron);

        jobs.AddOrUpdate<ScheduledJobs>(
            OutboxDrainJobId,
            job => job.RunOutboxDrainAsync(),
            options.OutboxDrainCron);

        jobs.AddOrUpdate<ScheduledJobs>(
            EmailDispatchJobId,
            job => job.RunEmailDispatchAsync(),
            options.EmailDispatchCron);

        // The webhook sweep has its own switch, like every other worker: when the transport is
        // off, the job stays out of the scheduler entirely rather than ticking over an empty
        // queue. That keeps "WebhookDispatch:Enabled = false" meaning the same thing under
        // Hangfire as it did under the worker's own timer.
        var webhookOptions = app.Services
            .GetRequiredService<IOptions<WebhookDispatchOptions>>().Value;

        if (webhookOptions.Enabled)
        {
            jobs.AddOrUpdate<ScheduledJobs>(
                WebhookDispatchJobId,
                job => job.RunWebhookDispatchAsync(),
                options.WebhookDispatchCron);
        }

        if (options.DashboardEnabled)
        {
            // The dashboard is the window onto a failed job and the only place a pass can be
            // re-run by hand. Access is gated by HangfireDashboardAuthorizationFilter: Production
            // (or any environment with DashboardUsername set) requires HTTP Basic credentials.
            app.MapHangfireDashboard(options.DashboardPath, new DashboardOptions
            {
                Authorization = [new HangfireDashboardAuthorizationFilter()]
            });
        }

        return app;
    }
}
