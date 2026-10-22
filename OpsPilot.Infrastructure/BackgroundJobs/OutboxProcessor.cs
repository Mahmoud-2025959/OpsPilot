using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Drains the outbox and hands each message to the publisher (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// <para>
/// The read half of the transactional outbox: handlers stage messages in the same transaction as
/// the change they describe, and this drains them afterwards. That is what turns "the incident
/// was stored" into "the integration event eventually goes out" without either having to wait
/// for the other.
/// </para>
/// <para>
/// One message failing does not stop the batch: it is retried on the next pass until it runs out
/// of attempts. Rows are marked, never deleted, because a message that could not be delivered is
/// something a person may have to act on.
/// </para>
/// </remarks>
public sealed class OutboxProcessor : BackgroundService
{
    /// <summary>
    /// Keeps a failure note inside the column that stores it: the exception a real transport
    /// throws can be far longer than two thousand characters, and a save that records why
    /// something failed must not itself fail.
    /// </summary>
    private const int MaxErrorLength = 2000;

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly OutboxProcessorOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopes,
        TimeProvider time,
        IOptions<OutboxProcessorOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _scopes = scopes;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbox dispatch started; draining every {Interval}.",
            _options.Interval);

        using var timer = new PeriodicTimer(_options.Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DispatchPendingAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down between passes; a pass commits as one batch, so there is no
            // half-written work to clean up.
        }
    }

    /// <summary>Publishes one batch, returning how many messages went out.</summary>
    /// <remarks>
    /// Public so a test can drive a pass instead of waiting on the timer.
    /// </remarks>
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        // A pass gets its own scope: the dispatcher runs for the process, while everything it
        // resolves lives per unit of work.
        using var scope = _scopes.CreateScope();

        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();

        var pending = await outbox.GetPendingAsync(_options.BatchSize, cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        var published = 0;

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(message.Type, message.Payload, cancellationToken);

                message.MarkProcessed(_time.GetUtcNow());
                published++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A failed publish is not the end of the message: it keeps its place until it
                // has used up its attempts, and only then is parked.
                RecordAttempt(message, exception);

                if (message.State is OutboxMessageState.Failed)
                {
                    _logger.LogError(
                        exception,
                        "Integration event {Type} (message {Id}) gave up after {Attempts} attempt(s).",
                        message.Type,
                        message.Id,
                        message.Attempts);
                }
                else
                {
                    _logger.LogWarning(
                        exception,
                        "Integration event {Type} (message {Id}) failed on attempt {Attempts}.",
                        message.Type,
                        message.Id,
                        message.Attempts);
                }
            }
        }

        await outbox.SaveChangesAsync(cancellationToken);

        return published;
    }

    private void RecordAttempt(OutboxMessage message, Exception exception)
    {
        var reason = Describe(exception);

        if (message.Attempts + 1 >= _options.MaxAttempts)
        {
            message.MarkFailed(reason);
        }
        else
        {
            message.RecordFailedAttempt(reason);
        }
    }

    private static string Describe(Exception exception) =>
        exception.Message.Length <= MaxErrorLength
            ? exception.Message
            : exception.Message[..MaxErrorLength];
}
