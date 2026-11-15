using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Features.SlaViolations.Commands.DetectSlaViolations;

namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs the SLA breach sweep on a timer, for every company in turn (plan, UC-12).
/// </summary>
/// <remarks>
/// <para>
/// The work itself lives in the application handler; this type only decides when it runs and
/// under which company. That split is what lets the rules be tested at a chosen instant rather
/// than by waiting on a timer.
/// </para>
/// <para>
/// It is deliberately forgiving: a company that fails is logged and skipped rather than
/// stopping the sweep, because one broken account must not leave every other company unwatched.
/// Re-running is safe, since a breach already on record is never recorded twice.
/// </para>
/// </remarks>
public sealed class SlaViolationDetectionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly SlaViolationWorkerOptions _options;
    private readonly ILogger<SlaViolationDetectionWorker> _logger;

    public SlaViolationDetectionWorker(
        IServiceScopeFactory scopes,
        TimeProvider time,
        IOptions<SlaViolationWorkerOptions> options,
        ILogger<SlaViolationDetectionWorker> logger)
    {
        _scopes = scopes;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SLA violation sweep started; a pass runs every {Interval}.",
            _options.Interval);

        using var timer = new PeriodicTimer(_options.Interval);

        try
        {
            // The first pass waits out one interval instead of running at startup: a rolling
            // deploy would otherwise have every instance sweep the whole estate at once, and
            // no deadline here is urgent to the second.
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var detected = await SweepOnceAsync(stoppingToken);

                if (detected > 0)
                {
                    _logger.LogInformation("Recorded {Count} SLA violation(s).", detected);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down: a pass is all-or-nothing per company, so there is nothing
            // half-done to clean up.
        }
    }

    /// <summary>
    /// One pass over every active company, returning how many breaches were recorded.
    /// </summary>
    /// <remarks>
    /// Each company is swept in its own scope with its own context, because the ambient tenant
    /// is baked into the EF model: a context that has already read one company's rows cannot be
    /// reused for another's (see <c>AppDbContext</c> and <c>TenantModelCacheKeyFactory</c>).
    /// </remarks>
    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var tenantIds = await ListTenantIdsAsync(cancellationToken);
        var detected = 0;

        foreach (var tenantId in tenantIds)
        {
            try
            {
                detected += await SweepAsync(tenantId, now, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "The SLA sweep failed for tenant {TenantId}; it will be retried on the next pass.",
                    tenantId);
            }
        }

        return detected;
    }

    private async Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken cancellationToken)
    {
        // Its own scope, and its own throwaway context: the list is company-wide, so this
        // context must not be the one a company is then swept with.
        using var scope = _scopes.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<ITenantDirectory>()
            .GetActiveIdsAsync(cancellationToken);
    }

    private async Task<int> SweepAsync(
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        // A background job has no request to inherit a tenant from, so it enters one before
        // anything tenant-scoped is resolved from this scope.
        using var entered = tenant.Enter(tenantId);

        return await scope.ServiceProvider
            .GetRequiredService<ISender>()
            .Send(new DetectSlaViolationsCommand(now), cancellationToken);
    }
}
