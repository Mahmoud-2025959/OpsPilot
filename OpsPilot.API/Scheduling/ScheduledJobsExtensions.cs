using Hangfire;
using Hangfire.Dashboard;
using Microsoft.Extensions.Options;
using OpsPilot.Infrastructure.BackgroundJobs;

namespace OpsPilot.API.Scheduling;

/// <summary>
/// Wires the recurring passes and, optionally, the Hangfire dashboard (plan,
/// "BackgroundJobs").
/// </summary>
/// <remarks>
/// <para>
/// Registration of the jobs themselves has to write to Hangfire storage, which is the
/// application database, so it cannot happen while the pipeline is being built — at that point
/// the migrator has not run yet. <see cref="AddScheduledJobsRegistration"/> therefore only
/// registers a hosted service, and
/// <see cref="ScheduledJobsRegistrationHostedService"/> performs the upserts once the host is
/// starting, after the schema exists.
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

    /// <summary>
    /// Registers the hosted service that writes the recurring jobs to storage when the host
    /// starts. No-op when Hangfire is off: neither the job manager nor the storage exists then,
    /// and the workers keep their own timers.
    /// </summary>
    public static IServiceCollection AddScheduledJobsRegistration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>()
            ?? new HangfireOptions();

        if (!options.Enabled)
        {
            return services;
        }

        // Registered after DatabaseMigratorHostedService and the Hangfire server (both of which
        // are added earlier), so it starts last and the storage is ready by the time it writes.
        services.AddHostedService<ScheduledJobsRegistrationHostedService>();

        return services;
    }

    /// <summary>Mounts the Hangfire dashboard when both the scheduler and the dashboard are on.</summary>
    public static WebApplication UseScheduledJobsDashboard(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<HangfireOptions>>().Value;

        if (!options.Enabled || !options.DashboardEnabled)
        {
            return app;
        }

        // The dashboard is the window onto a failed job and the only place a pass can be
        // re-run by hand. Access is gated by HangfireDashboardAuthorizationFilter: Production
        // (or any environment with DashboardUsername set) requires HTTP Basic credentials.
        app.MapHangfireDashboard(options.DashboardPath, new DashboardOptions
        {
            Authorization = [new HangfireDashboardAuthorizationFilter()]
        });

        return app;
    }
}
