using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Delivers queued webhook calls, on a timer, for every company in turn (plan, notifications
/// group: the webhook channel's delivery step).
/// </summary>
/// <remarks>
/// <para>
/// The write half of the webhook channel: the event that is being announced stages a row in
/// the same transaction as itself, and this turns those rows into calls afterwards. That is
/// the same shape as the email channel and the outbox, for the same reason — something must
/// eventually leave the system without either side waiting on the other.
/// </para>
/// <para>
/// The retry rules are borrowed from the email dispatcher wholesale: one failure does not
/// stop the batch, rows keep their place until they run out of attempts, and rows are marked
/// rather than deleted.
/// </para>
/// </remarks>
public sealed class WebhookDispatchWorker : BackgroundService
{
    /// <summary>
    /// Keeps a failure note inside the column that stores it: a reason can be far longer than
    /// any column should hold, and truncating it here keeps a recording of why a call failed
    /// from becoming a failure of its own.
    /// </summary>
    private const int MaxErrorLength = 2000;

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly WebhookDispatchOptions _options;
    private readonly ILogger<WebhookDispatchWorker> _logger;

    public WebhookDispatchWorker(
        IServiceScopeFactory scopes,
        TimeProvider time,
        IOptions<WebhookDispatchOptions> options,
        ILogger<WebhookDispatchWorker> logger)
    {
        _scopes = scopes;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Webhook dispatch started; a pass runs every {Interval}.",
            _options.Interval);

        using var timer = new PeriodicTimer(_options.Interval);

        try
        {
            // The first pass waits out one interval instead of running at startup, same as the
            // email sweep: a rolling deploy would otherwise have every instance drain the queue
            // at once, and a queued call is not urgent to the second.
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var delivered = await DispatchPendingAsync(stoppingToken);

                if (delivered > 0)
                {
                    _logger.LogInformation("Delivered {Count} webhook call(s).", delivered);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down between passes: a pass commits as one save per company, so there
            // is no half-written work to clean up.
        }
    }

    /// <summary>One pass over every active company, returning how many calls went out.</summary>
    /// <remarks>
    /// Public so a test can drive a pass instead of waiting on the timer — the same reason
    /// <c>OutboxProcessor.DispatchPendingAsync</c> is.
    /// </remarks>
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await ListTenantIdsAsync(cancellationToken);
        var delivered = 0;

        foreach (var tenantId in tenantIds)
        {
            try
            {
                delivered += await DispatchAsync(tenantId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One company's failure must not stop the sweep: every other company's calls
                // still go out, and this one is retried on the next pass.
                _logger.LogError(
                    exception,
                    "Webhook dispatch failed for tenant {TenantId}; it will be retried on the next pass.",
                    tenantId);
            }
        }

        return delivered;
    }

    private async Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken cancellationToken)
    {
        // Its own scope, and its own throwaway context: the list is company-wide, so this
        // context must not be the one a company is then dispatched with.
        using var scope = _scopes.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<ITenantDirectory>()
            .GetActiveIdsAsync(cancellationToken);
    }

    private async Task<int> DispatchAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        // A background job has no request to inherit a tenant from, so it enters one before
        // anything tenant-scoped is resolved from this scope — the ambient tenant is baked
        // into the EF model at the first query.
        using var entered = tenant.Enter(tenantId);

        var webhooks = scope.ServiceProvider.GetRequiredService<IWebhookRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<IWebhookSender>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var pending = await webhooks.GetPendingDeliveriesAsync(_options.BatchSize, cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        var now = _time.GetUtcNow();
        var delivered = 0;

        foreach (var (delivery, endpoint) in pending)
        {
            if (!endpoint.IsActive)
            {
                // Not retried: the endpoint was put out of service on purpose, and it will
                // still be out of service on the next pass.
                delivery.MarkFailed("The endpoint is not active.");

                _logger.LogWarning(
                    "Webhook delivery {Id} was not made; the endpoint is not active.",
                    delivery.Id);

                continue;
            }

            var secret = protector.Unprotect(endpoint.SecretCiphertext);

            if (string.IsNullOrWhiteSpace(secret))
            {
                // An endpoint registered before the secret began being stored has none to
                // sign with, and a hash cannot sign anything. Rotating it is what gives it one.
                delivery.MarkFailed(
                    "The endpoint has no stored signing secret. Rotate it to get one.");

                _logger.LogWarning(
                    "Webhook delivery {Id} was not made; the endpoint has no signing secret.",
                    delivery.Id);

                continue;
            }

            try
            {
                await sender.SendAsync(
                    new WebhookDeliveryRequest(
                        endpoint.Url,
                        delivery.EventType,
                        delivery.Id.ToString(),
                        delivery.Payload,
                        secret,
                        now),
                    cancellationToken);

                delivery.MarkDelivered(now);
                delivered++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A failed call is not the end of the row: it keeps its place until it has used
                // up its attempts, and only then is parked. One row failing does not stop the
                // rest of the batch.
                RecordAttempt(delivery, exception);

                if (delivery.Status is Domain.Enums.WebhookDeliveryStatus.Failed)
                {
                    _logger.LogError(
                        exception,
                        "Webhook delivery {Id} gave up after {Attempts} attempt(s).",
                        delivery.Id,
                        delivery.Attempts);
                }
                else
                {
                    _logger.LogWarning(
                        exception,
                        "Webhook delivery {Id} failed on attempt {Attempts}.",
                        delivery.Id,
                        delivery.Attempts);
                }
            }
        }

        await webhooks.SaveDeliveriesAsync(cancellationToken);

        return delivered;
    }

    private void RecordAttempt(WebhookDelivery delivery, Exception exception)
    {
        var reason = Describe(exception);

        // A refusal that will keep happening — a 4xx, a URL that cannot be used — earns no
        // more attempts. Anything else (a 5xx, a timeout, a socket reset) does.
        var permanent = exception is WebhookDeliveryException failure && failure.IsPermanent;

        if (permanent || delivery.Attempts + 1 >= _options.MaxAttempts)
        {
            delivery.MarkFailed(reason);
        }
        else
        {
            delivery.RecordFailedAttempt(reason);
        }
    }

    private static string Describe(Exception exception) =>
        exception.Message.Length <= MaxErrorLength
            ? exception.Message
            : exception.Message[..MaxErrorLength];
}
