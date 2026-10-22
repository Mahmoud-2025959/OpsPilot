using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OpsPilot.Infrastructure.RabbitMQ;

/// <summary>
/// Owns the single AMQP connection the application publishes over (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// <para>
/// The publisher is registered per scope like every other repository, but an AMQP connection is
/// expensive to open and holds server-side resources, so it is opened once and shared. Channels
/// are not shared: they are not thread-safe, so each publisher keeps its own.
/// </para>
/// <para>
/// The connection is opened lazily on first use rather than in the constructor. A broker that is
/// down when the application starts would otherwise take the whole host down with it, when the
/// correct behaviour is for the outbox dispatcher to record failed attempts and keep the row
/// waiting — which is exactly what it already does with any publisher that throws.
/// </para>
/// <para>
/// The topology (exchange, dead-letter exchange and its queue) is declared on every successful
/// channel because declaration is idempotent: a container that forgot its volumes comes back
/// with an empty broker, and this is what stops the first publish from failing on a missing
/// exchange.
/// </para>
/// </remarks>
public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConnection> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IConnection? _connection;

    public RabbitMqConnection(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConnection> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Opens a channel with the topology already declared.</summary>
    public async Task<IChannel> CreateChannelAsync(
        CreateChannelOptions? channelOptions = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);

        await channel.ExchangeDeclareAsync(
            _options.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        // The dead-letter side is declared up front so the branch that routes to it never has to
        // create it mid-failure, when the broker is least likely to be cooperative. Exchanges
        // route undeliverable messages here via the queue's x-dead-letter-exchange argument,
        // which a consumer sets when it declares its own queue.
        await channel.ExchangeDeclareAsync(
            _options.DeadLetterExchangeName,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            _options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            _options.DeadLetterQueueName,
            _options.DeadLetterExchangeName,
            routingKey: string.Empty,
            arguments: null,
            noWait: false,
            cancellationToken: cancellationToken);

        return channel;
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        // Fast path: an open connection is returned without taking the lock, so the common publish
        // never queues behind it.
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            // A broken connection is closed before being dropped so the client library releases
            // its socket rather than leaking it until finalisation.
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,
                // A broker restart or a network blip should heal without a host restart; the
                // client reconnects underneath as long as it is allowed to.
                AutomaticRecoveryEnabled = true
            };

            if (_options.UseTls)
            {
                factory.Ssl = new SslOption { Enabled = true, ServerName = _options.HostName };
            }

            _connection = await factory.CreateConnectionAsync(
                clientProvidedName: "OpsPilot",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Connected to RabbitMQ at {Host}:{Port}/{VirtualHost}.",
                _options.HostName,
                _options.Port,
                _options.VirtualHost);

            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        _gate.Dispose();
    }
}
