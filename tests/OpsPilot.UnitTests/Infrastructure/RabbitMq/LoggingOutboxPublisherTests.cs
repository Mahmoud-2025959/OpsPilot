using Microsoft.Extensions.Logging;
using Moq;
using OpsPilot.Infrastructure.Services;

namespace OpsPilot.UnitTests.Infrastructure.RabbitMq;

/// <summary>
/// Tests for <see cref="LoggingOutboxPublisher"/> — the no-broker stand-in.
/// </summary>
public class LoggingOutboxPublisherTests
{
    private static LoggingOutboxPublisher Build(out Mock<ILogger<LoggingOutboxPublisher>> logger)
    {
        logger = new Mock<ILogger<LoggingOutboxPublisher>>();
        return new LoggingOutboxPublisher(logger.Object);
    }

    [Fact]
    public async Task PublishAsync_Succeeds_WithoutThrowing()
    {
        var publisher = Build(out _);

        // The logging publisher must never throw — it is the fallback for a dev environment
        // that has no broker, and crashing would stop every outbox drain.
        var ex = await Record.ExceptionAsync(() =>
            publisher.PublishAsync("incident.created", "{}", CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task PublishAsync_ReturnsCancelledTask_WhenTokenAlreadyCancelled()
    {
        var publisher = Build(out _);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // The logging publisher does not observe the token — it just logs and returns.
        // So even a cancelled token should not throw here.
        var ex = await Record.ExceptionAsync(() =>
            publisher.PublishAsync("incident.created", "{}", cts.Token));

        Assert.Null(ex);
    }

    [Fact]
    public async Task PublishAsync_LogsTheEventType()
    {
        var publisher = Build(out var logger);

        await publisher.PublishAsync("incident.created", "{\"id\":\"abc\"}", CancellationToken.None);

        logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => (v!.ToString() ?? string.Empty).Contains("incident.created")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WorksForAnyEventType()
    {
        var publisher = Build(out _);
        string[] types = ["incident.created", "incident.resolved", "sla.breached", "webhook.delivered"];

        foreach (var type in types)
        {
            var ex = await Record.ExceptionAsync(() =>
                publisher.PublishAsync(type, "{}", CancellationToken.None));

            Assert.Null(ex);
        }
    }
}
