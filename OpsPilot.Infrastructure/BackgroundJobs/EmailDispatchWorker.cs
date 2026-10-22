using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Delivers queued notification emails over SMTP, on a timer, for every company in turn
/// (plan, notifications group: the email channel's delivery step).
/// </summary>
/// <remarks>
/// <para>
/// The write half of the email channel: the notification service stages a Pending row in the
/// same transaction as the incident it describes, and this turns those rows into mail
/// afterwards. That is the same shape as the outbox dispatcher because the problem is the
/// same - something must eventually leave the system without either side waiting on the
/// other - so the retry rules are borrowed from it wholesale: one failure does not stop the
/// batch, rows keep their place until they run out of attempts, and rows are marked rather
/// than deleted.
/// </para>
/// <para>
/// With the transport switched off the queue is left alone entirely: attempts are not burned
/// on an installation that has not configured SMTP yet, and everything waiting is delivered
/// once it does. The recipient address is read at send time rather than stamped on the row,
/// so a person who corrected their email between the alert and the send gets the corrected one.
/// </para>
/// </remarks>
public sealed class EmailDispatchWorker : BackgroundService
{
    /// <summary>
    /// Keeps a failure note inside the column that stores it: an SMTP reason can be far longer
    /// than two thousand characters, and a save that records why delivery failed must not
    /// itself fail.
    /// </summary>
    private const int MaxErrorLength = 2000;

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly EmailDispatchOptions _options;
    private readonly SmtpOptions _smtp;
    private readonly ILogger<EmailDispatchWorker> _logger;

    public EmailDispatchWorker(
        IServiceScopeFactory scopes,
        TimeProvider time,
        IOptions<EmailDispatchOptions> options,
        IOptions<SmtpOptions> smtp,
        ILogger<EmailDispatchWorker> logger)
    {
        _scopes = scopes;
        _time = time;
        _options = options.Value;
        _smtp = smtp.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Email dispatch started; a pass runs every {Interval}.",
            _options.Interval);

        using var timer = new PeriodicTimer(_options.Interval);

        try
        {
            // The first pass waits out one interval instead of running at startup, same as the
            // SLA sweep: a rolling deploy would otherwise have every instance drain the queue
            // at once, and no queued email is urgent to the second.
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var delivered = await DispatchPendingEmailsAsync(stoppingToken);

                if (delivered > 0)
                {
                    _logger.LogInformation("Delivered {Count} notification email(s).", delivered);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down between passes: a pass commits as one save per company, so there
            // is no half-written work to clean up.
        }
    }

    /// <summary>One pass over every active company, returning how many emails went out.</summary>
    /// <remarks>
    /// Public so a test can drive a pass instead of waiting on the timer - the same reason
    /// <c>OutboxProcessor.DispatchPendingAsync</c> is.
    /// </remarks>
    public async Task<int> DispatchPendingEmailsAsync(CancellationToken cancellationToken = default)
    {
        if (!_smtp.Enabled)
        {
            // Not an error and not an attempt: rows keep waiting, so switching the transport
            // on later delivers everything that piled up meanwhile.
            _logger.LogDebug(
                "SMTP transport is switched off (Smtp:Enabled); queued emails stay pending.");
            return 0;
        }

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
                // One company's failure must not stop the sweep: every other company's mail
                // still goes out, and this one is retried on the next pass. The SLA sweep takes
                // the same stance, for the same reason.
                _logger.LogError(
                    exception,
                    "Email dispatch failed for tenant {TenantId}; it will be retried on the next pass.",
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
        // anything tenant-scoped is resolved from this scope - the ambient tenant is baked
        // into the EF model at first query.
        using var entered = tenant.Enter(tenantId);

        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var pending = await notifications.GetPendingEmailsAsync(
            _options.BatchSize,
            cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        var now = _time.GetUtcNow();
        var delivered = 0;

        foreach (var (notification, recipient) in pending)
        {
            if (string.IsNullOrWhiteSpace(recipient))
            {
                // Not retried: the address is read at send time on purpose, and a recipient
                // that does not resolve now will not resolve on the next pass either. Parking
                // it keeps it out of the queue while leaving the row - and its reason - for a
                // person to read.
                notification.MarkFailed("The recipient has no deliverable address.");

                _logger.LogWarning(
                    "Notification {Id} has no deliverable address; parked as failed.",
                    notification.Id);

                continue;
            }

            try
            {
                await sender.SendAsync(
                    new EmailMessage(recipient, notification.Title, notification.Body),
                    cancellationToken);

                notification.MarkSent(now);
                delivered++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A failed send is not the end of the row: it keeps its place until it has used
                // up its attempts, and only then is parked. One row failing does not stop the
                // rest of the batch.
                RecordAttempt(notification, exception);

                if (notification.Status is NotificationStatus.Failed)
                {
                    _logger.LogError(
                        exception,
                        "Notification email {Id} gave up after {Attempts} attempt(s).",
                        notification.Id,
                        notification.Attempts);
                }
                else
                {
                    _logger.LogWarning(
                        exception,
                        "Notification email {Id} failed on attempt {Attempts}.",
                        notification.Id,
                        notification.Attempts);
                }
            }
        }

        await notifications.SaveDeliveriesAsync(cancellationToken);

        return delivered;
    }

    private void RecordAttempt(Notification notification, Exception exception)
    {
        var reason = Describe(exception);

        // A 5xx reply is the server saying this will never work - a rejected address, refused
        // credentials - and five more passes would only delay the person who has to fix it.
        // Anything else (a 4xx, a timeout, a socket reset) earns its retries.
        if (IsPermanent(exception) || notification.Attempts + 1 >= _options.MaxAttempts)
        {
            notification.MarkFailed(reason);
        }
        else
        {
            notification.RecordFailedAttempt(reason);
        }
    }

    private static bool IsPermanent(Exception exception) =>
        exception is SmtpCommandException command && (int)command.StatusCode >= 500;

    private static string Describe(Exception exception) =>
        exception.Message.Length <= MaxErrorLength
            ? exception.Message
            : exception.Message[..MaxErrorLength];
}
