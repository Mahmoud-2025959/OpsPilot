using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// The bodies Hangfire runs on a schedule (plan, "BackgroundJobs").
/// </summary>
/// <remarks>
/// <para>
/// This is the seam between the scheduler and the work. Each method does nothing but hand off to
/// the pass method the worker has always exposed — <c>SweepOnceAsync</c>,
/// <c>DispatchPendingAsync</c>, <c>DispatchPendingEmailsAsync</c> — so the rules, the retry
/// bookkeeping and the tenant scoping stay exactly where they were and where they are tested.
/// </para>
/// <para>
/// It resolves the workers from a scope per invocation because Hangfire runs the job body outside
/// any request: the workers take an <see cref="IServiceScopeFactory"/> and create their own
/// per-company scopes from it, so the only thing this class needs is to ask for them.
/// </para>
/// <para>
/// Every method returns the count it did so the number shows up in the Hangfire dashboard next to
/// the run, which is a big part of why the dashboard is worth having.
/// </para>
/// </remarks>
public sealed class ScheduledJobs
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ScheduledJobs> _logger;

    public ScheduledJobs(IServiceScopeFactory scopes, ILogger<ScheduledJobs> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>One SLA breach sweep over every active company.</summary>
    public async Task<int> RunSlaSweepAsync()
    {
        using var scope = _scopes.CreateScope();

        var worker = scope.ServiceProvider.GetRequiredService<SlaViolationDetectionWorker>();

        var detected = await worker.SweepOnceAsync();

        if (detected > 0)
        {
            _logger.LogInformation("Recorded {Count} SLA violation(s).", detected);
        }

        return detected;
    }

    /// <summary>One outbox drain over the whole queue.</summary>
    public async Task<int> RunOutboxDrainAsync()
    {
        using var scope = _scopes.CreateScope();

        var worker = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

        return await worker.DispatchPendingAsync();
    }

    /// <summary>One email dispatch over every active company.</summary>
    public async Task<int> RunEmailDispatchAsync()
    {
        using var scope = _scopes.CreateScope();

        var worker = scope.ServiceProvider.GetRequiredService<EmailDispatchWorker>();

        var delivered = await worker.DispatchPendingEmailsAsync();

        if (delivered > 0)
        {
            _logger.LogInformation("Delivered {Count} notification email(s).", delivered);
        }

        return delivered;
    }
}
