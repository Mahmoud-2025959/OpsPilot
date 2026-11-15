namespace OpsPilot.Infrastructure.RabbitMQ;

/// <summary>
/// Connection and topology settings for the broker the outbox publishes to (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// <para>
/// The broker is optional, exactly like the mail transport: with <see cref="Enabled"/> false the
/// logging publisher stays wired and the rest of the pipeline behaves as it always did. That is
/// what lets a developer run the solution without a RabbitMQ container, and what lets the test
/// host keep a real socket out of its assertions.
/// </para>
/// <para>
/// <see cref="Password"/> is a credential and is never committed: it binds from configuration
/// like the SMTP password does, so in development it comes from user-secrets and in deployment
/// from the environment.
/// </para>
/// </remarks>
public sealed class RabbitMqOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// False keeps the broker publisher out and leaves the logging one wired. Off by default so
    /// that a fresh clone starts without requiring a container to be running.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Host name of the broker.</summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>AMQP port. 5672 is the plain one; 5671 is TLS.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>Virtual host to connect to.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>Broker user. Defaults to the one a vanilla RabbitMQ image provisions.</summary>
    public string UserName { get; set; } = "guest";

    /// <summary>Broker password. Supplied from user-secrets or the environment, never the repo.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Whether to negotiate TLS. Off for a local container, on for a managed broker over the
    /// public network.
    /// </summary>
    public bool UseTls { get; set; }

    /// <summary>
    /// The topic exchange integration events are published to. A topic exchange is chosen over a
    /// direct one because routing keys in this system are dotted types
    /// (<c>incident.created</c>), and a consumer will want to subscribe to a family of them
    /// (<c>incident.#</c>) rather than name each one.
    /// </summary>
    public string ExchangeName { get; set; } = "opspilot.events";

    /// <summary>
    /// The queue undeliverable messages are parked on. Declared alongside the exchange so a dead
    /// letter has somewhere to go the moment the first publisher failure happens.
    /// </summary>
    public string DeadLetterExchangeName { get; set; } = "opspilot.events.dlx";

    /// <summary>The queue bound to the dead-letter exchange.</summary>
    public string DeadLetterQueueName { get; set; } = "opspilot.events.dead";

    /// <summary>
    /// How long a publish may block waiting for the broker to confirm it. A broker that accepts
    /// the write but never confirms must not hold the drain open indefinitely.
    /// </summary>
    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// When true, the integration consumer subscribes to the integration queue and applies the
    /// messages it receives. Off by default, for the same reason the publisher is: a fresh clone
    /// starts without requiring a broker to be running.
    /// </summary>
    public bool ConsumerEnabled { get; set; }

    /// <summary>
    /// The queue the integration consumer reads from. Bound to the exchange with a wildcard, so
    /// it sees every event type rather than one family of them.
    /// </summary>
    public string ConsumerQueueName { get; set; } = "opspilot.integration";

    /// <summary>
    /// How many messages the broker may have in flight for this consumer at once. Low on purpose:
    /// each message is applied inside a transaction, and a wider window would let one slow
    /// company hold the whole queue.
    /// </summary>
    public ushort ConsumerPrefetch { get; set; } = 10;
}
