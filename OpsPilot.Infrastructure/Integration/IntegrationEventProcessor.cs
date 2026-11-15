using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Messages;
using OpsPilot.Infrastructure.Persistence;

namespace OpsPilot.Infrastructure.Integration;

/// <summary>
/// What became of a message that was handed to the processor.
/// </summary>
public enum IntegrationEventOutcome
{
    /// <summary>The message was applied.</summary>
    Handled,

    /// <summary>The message had already been claimed, so it was skipped.</summary>
    Duplicate,

    /// <summary>No handler is registered for this kind of event.</summary>
    NoHandler
}

/// <summary>
/// Applies an integration event that arrived over the broker (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// <para>
/// The claim in the ledger and the work behind it happen inside one transaction: either both
/// stand or neither does. That is what makes a failed message retryable — the claim is rolled
/// back with the work, so the redelivery is applied rather than skipped as a duplicate.
/// </para>
/// <para>
/// The company comes from the envelope rather than from the environment: a consumer has no
/// request to inherit a tenant from, and the message says which company it is about.
/// </para>
/// </remarks>
public sealed class IntegrationEventProcessor
{
    private readonly AppDbContext _context;
    private readonly IInboxRepository _inbox;
    private readonly IEnumerable<IIntegrationEventHandler> _handlers;
    private readonly TimeProvider _time;
    private readonly ILogger<IntegrationEventProcessor> _logger;

    public IntegrationEventProcessor(
        AppDbContext context,
        IInboxRepository inbox,
        IEnumerable<IIntegrationEventHandler> handlers,
        TimeProvider time,
        ILogger<IntegrationEventProcessor> logger)
    {
        _context = context;
        _inbox = inbox;
        _handlers = handlers;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    /// Applies one message.
    /// </summary>
    /// <exception cref="ArgumentException">The message has no id to claim it by.</exception>
    public async Task<IntegrationEventOutcome> ProcessAsync(
        string messageId,
        IntegrationEventMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentNullException.ThrowIfNull(message);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (!await _inbox.TryBeginAsync(messageId, IntegrationConsumers.Inbound, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogInformation(
                "Integration message {MessageId} was already claimed; it is skipped.",
                messageId);

            return IntegrationEventOutcome.Duplicate;
        }

        var handler = _handlers.FirstOrDefault(h =>
            string.Equals(h.Type, message.Type, StringComparison.OrdinalIgnoreCase));

        if (handler is null)
        {
            // Recorded and acknowledged rather than retried: an event nobody answers is not a
            // failure, and retrying it forever would keep a message in the queue for good.
            _logger.LogInformation(
                "No handler is registered for integration event {Type}; the message is recorded and acknowledged.",
                message.Type);
        }
        else
        {
            await handler.HandleAsync(message, cancellationToken);
        }

        await _inbox.MarkProcessedAsync(
            messageId,
            IntegrationConsumers.Inbound,
            _time.GetUtcNow(),
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return handler is null ? IntegrationEventOutcome.NoHandler : IntegrationEventOutcome.Handled;
    }
}
