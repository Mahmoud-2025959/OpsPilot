using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using RabbitMQ.Client;

namespace OpsPilot.Infrastructure.RabbitMQ;

/// <summary>
/// Publishes staged integration messages to RabbitMQ (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// <para>
/// This is the real transport behind <see cref="IOutboxPublisher"/>. It replaces the logging
/// stand-in without anything else changing: the dispatcher, the entity and the handlers all
/// speak to the interface, so none of them knows a broker is involved now.
/// </para>
/// <para>
/// Publisher confirms are on, and a publish waits for the broker's acknowledgement. That is
/// deliberate: without it "sent" would only mean "written to a socket", and the outbox would mark
/// a message processed that the broker may never have stored. Waiting is what makes
/// <see cref="Domain.Entities.OutboxMessage.MarkProcessed"/> an honest statement.
/// </para>
/// <para>
/// Messages are published persistent (delivery mode 2). Combined with a durable exchange, that
/// means a broker restart does not drop what was already accepted.
/// </para>
/// </remarks>
public sealed class RabbitMqOutboxPublisher : IOutboxPublisher, IAsyncDisposable
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqOutboxPublisher> _logger;

    private IChannel? _channel;

    public RabbitMqOutboxPublisher(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqOutboxPublisher> logger)
    {
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(
        string type,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var channel = await GetChannelAsync(cancellationToken);

        // The outbox type doubles as the routing key. Types in this system are already dotted
        // (<c>incident.created</c>), which is exactly topic-exchange vocabulary, so a consumer
        // can bind to a family (<c>incident.#</c>) without the publisher knowing who listens.
        var routingKey = type;

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            Type = type,
            MessageId = Guid.NewGuid().ToString()
        };

        var body = Encoding.UTF8.GetBytes(payload);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.PublishTimeout);

        try
        {
            // mandatory: false — a message no queue has bound to is still a valid event. The
            // outbox exists to announce what happened, not to guarantee a reader is present, and
            // failing the publish because nobody subscribed would park messages that were, in
            // fact, delivered to the exchange successfully.
            await channel.BasicPublishAsync(
                _options.ExchangeName,
                routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: timeout.Token);

            _logger.LogDebug("Published integration event {Type} to {Exchange}.", type, _options.ExchangeName);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The linked token fired, not the caller's: the broker accepted the write but never
            // confirmed it in time. Surfaced as a failure so the dispatcher keeps the row and
            // tries again rather than marking it processed on a guess.
            throw new TimeoutException(
                $"RabbitMQ did not confirm the publish of '{type}' within {_options.PublishTimeout}.");
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        // A channel that died with its connection cannot be reused; it is closed and replaced.
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        // Confirms are what make a successful publish mean the broker has the message.
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        _channel = await _connection.CreateChannelAsync(options, cancellationToken);

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
    }
}
