using Hangfire;
using Microsoft.Extensions.Options;
using OpsPilot.Infrastructure.BackgroundJobs;

namespace OpsPilot.API.Scheduling;

/// <summary>
/// Writes the recurring passes into Hangfire storage when the host starts, after the schema
/// exists.
/// </summary>
/// <remarks>
/// <para>
/// A recurring job is a row in the application database, so registering one is a write. It used
/// to happen while the request pipeline was being built, which meant it ran before
/// <see cref="DatabaseMigratorHostedService"/> had created the database and applied the
/// migrations: the first start against an empty database failed inside
/// <c>RecurringJob.AddOrUpdate</c> and the host never came up. As a hosted service, registered
/// after the migrator and after the Hangfire server, the write only happens once the storage is
/// ready.
/// </para>
/// <para>
/// The registration is an upsert keyed by a stable id, so a changed cron in configuration takes
/// effect on the next deploy rather than stacking a second job beside the old one.
/// </para>
/// </remarks>
public sealed class ScheduledJobsRegistrationHostedService : IHostedService
{
    private readonly IRecurringJobManager _jobs;
    private readonly HangfireOptions _options;
    private readonly WebhookDispatchOptions _webhookOptions;
    private readonly ILogger<ScheduledJobsRegistrationHostedService> _logger;

    public ScheduledJobsRegistrationHostedService(
        IRecurringJobManager jobs,
        IOptions<HangfireOptions> options,
        IOptions<WebhookDispatchOptions> webhookOptions,
        ILogger<ScheduledJobsRegistrationHostedService> logger)
    {
        _jobs = jobs;
        _options = options.Value;
        _webhookOptions = webhookOptions.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // The job body is a method on ScheduledJobs, so Hangfire resolves it from the container
        // per run and the actual work stays in the workers it delegates to.
        _jobs.AddOrUpdate<ScheduledJobs>(
            ScheduledJobsExtensions.SlaSweepJobId,
            job => job.RunSlaSweepAsync(),
            _options.SlaSweepCron);

        _jobs.AddOrUpdate<ScheduledJobs>(
            ScheduledJobsExtensions.OutboxDrainJobId,
            job => job.RunOutboxDrainAsync(),
            _options.OutboxDrainCron);

        _jobs.AddOrUpdate<ScheduledJobs>(
            ScheduledJobsExtensions.EmailDispatchJobId,
            job => job.RunEmailDispatchAsync(),
            _options.EmailDispatchCron);

        // The webhook sweep has its own switch, like every other worker: when the transport is
        // off, the job stays out of the scheduler entirely rather than ticking over an empty
        // queue.
        if (_webhookOptions.Enabled)
        {
            _jobs.AddOrUpdate<ScheduledJobs>(
                ScheduledJobsExtensions.WebhookDispatchJobId,
                job => job.RunWebhookDispatchAsync(),
                _options.WebhookDispatchCron);
        }

        _logger.LogInformation(
            "Registered {Count} recurring job(s) with Hangfire.",
            _webhookOptions.Enabled ? 4 : 3);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
