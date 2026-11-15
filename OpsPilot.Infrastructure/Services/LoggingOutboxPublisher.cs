using Microsoft.Extensions.Logging;
using OpsPilot.Application.Abstractions;

namespace OpsPilot.Infrastructure.Services;

/// <summary>
/// The publishing step while no broker is wired (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// It reports success and writes the message to the log, so the rest of the pipeline — staging,
/// attempting, marking — can be exercised end to end exactly as a real transport would. Putting
/// a bus behind this means replacing this registration and nothing else: no handler and no part
/// of the dispatcher names one.
/// </remarks>
public sealed class LoggingOutboxPublisher : IOutboxPublisher
{
    private readonly ILogger<LoggingOutboxPublisher> _logger;

    public LoggingOutboxPublisher(ILogger<LoggingOutboxPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(string type, string payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Published integration event {Type}: {Payload}", type, payload);

        return Task.CompletedTask;
    }
}
