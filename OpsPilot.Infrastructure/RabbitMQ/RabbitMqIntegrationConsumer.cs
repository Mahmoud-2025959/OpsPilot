using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Messages;
using OpsPilot.Infrastructure.Integration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OpsPilot.Infrastructure.RabbitMQ;

/// <summary>
/// Reads integration events off the broker and applies them (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// <para>
/// The other end of the outbox. A message that could not be delivered leaves a row behind and
/// is published again; a message that was published is consumed here, applied, and acknowledged.
/// The broker promises at-least-once delivery, so the work behind a message is protected by the
/// ledger of consumed messages rather than by the broker's word.
/// </para>
/// <para>
/// Every message is applied in its own scope and its own company: the company is named by the
/// envelope, because a consumer has no request to inherit a tenant from.
/// </para>
/// <para>
/// A message that cannot be read is parked rather than retried — a producer that got the
/// format wrong will get it wrong again, and a queue that keeps a message forever is a queue
/// nobody can drain. A message that fails is given one more delivery; after that it is parked
/// with the reason in the log, for a person to look at.
/// </para>
/// </remarks>
public sealed class RabbitMqIntegrationConsumer : BackgroundService
{
    /// <summary>How long to wait before trying to connect again after a failure.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RabbitMqIntegrationConsumer> _logger;

    public RabbitMqIntegrationConsumer(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopes,
        ILogger<RabbitMqIntegrationConsumer> logger)
    {
        _connection = connection;
        _options = options.Value;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A broker that is down must not take the host down with it: the rest of the
                // application carries on, and the consumer tries again.
                _logger.LogError(
                    exception,
                    "The integration consumer could not read from the broker; it tries again in {Delay}.",
                    RetryDelay);

                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // The queue parks what it cannot deliver: the dead-letter exchange is declared
        // alongside the main one, so the branch that routes to it is there from the start.
        await channel.QueueDeclareAsync(
            queue: _options.ConsumerQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName
            },
            passive: false,
            noWait: false,
            cancellationToken: stoppingToken);

        // A wildcard, so the queue sees every kind of event rather than one family of them.
        await channel.QueueBindAsync(
            queue: _options.ConsumerQueueName,
            exchange: _options.ExchangeName,
            routingKey: "#",
            arguments: null,
            noWait: false,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _options.ConsumerPrefetch,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += (_, args) => HandleAsync(channel, args, stoppingToken);

        await channel.BasicConsumeAsync(
            queue: _options.ConsumerQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "The integration consumer is reading from {Queue}.",
            _options.ConsumerQueueName);

        // Nothing else to do: the callback drives the work until the host shuts down.
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        CancellationToken stoppingToken)
    {
        var messageId = args.BasicProperties?.MessageId;

        if (string.IsNullOrWhiteSpace(messageId))
        {
            // Nothing to claim the message by, so nothing can recognise a redelivery of it.
            // It is applied once, under an id of its own, rather than dropped.
            messageId = Guid.NewGuid().ToString("N");
        }

        var body = Encoding.UTF8.GetString(args.Body.Span);

        if (!IntegrationEventMessage.TryParse(body, out var message) || message is null)
        {
            _logger.LogWarning(
                "An integration message could not be read; it is parked without being applied.");

            await channel.BasicNackAsync(
                deliveryTag: args.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: stoppingToken);

            return;
        }

        try
        {
            await ApplyAsync(messageId, message, stoppingToken);

            await channel.BasicAckAsync(
                deliveryTag: args.DeliveryTag,
                multiple: false,
                cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down: the message is left unacknowledged, so the broker gives it to
            // whoever is running next.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Integration message {MessageId} could not be applied.",
                messageId);

            // One more delivery, and then it is parked. The broker's own redelivery flag is
            // what counts the attempts, so no extra state has to be kept for it.
            await channel.BasicNackAsync(
                deliveryTag: args.DeliveryTag,
                multiple: false,
                requeue: !args.Redelivered,
                cancellationToken: stoppingToken);
        }
    }

    private async Task ApplyAsync(
        string messageId,
        IntegrationEventMessage message,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();

        // The company has to be established before anything tenant-scoped is resolved from this
        // scope: the ambient tenant is baked into the EF model at the first query.
        var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        using var entered = tenant.Enter(message.TenantId);

        var processor = scope.ServiceProvider.GetRequiredService<IntegrationEventProcessor>();

        await processor.ProcessAsync(messageId, message, cancellationToken);
    }
}
