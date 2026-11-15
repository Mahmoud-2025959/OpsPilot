using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.BackgroundJobs;

namespace OpsPilot.UnitTests.Infrastructure.Hangfire;

/// <summary>
/// Tests for <see cref="OutboxProcessor.DispatchPendingAsync"/> — the outbox drain pass.
/// </summary>
public class OutboxProcessorTests
{
    // ── factory helpers ───────────────────────────────────────────────────────

    private static OutboxProcessor BuildProcessor(
        IOutboxRepository outbox,
        IOutboxPublisher publisher,
        OutboxProcessorOptions? options = null)
    {
        var opts = options ?? new OutboxProcessorOptions { Enabled = true, BatchSize = 50, MaxAttempts = 5 };

        // Scope factory that serves the mocked repo and publisher.
        var services = new ServiceCollection();
        services.AddSingleton<IOutboxRepository>(outbox);
        services.AddSingleton<IOutboxPublisher>(publisher);

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new OutboxProcessor(
            scopeFactory,
            TimeProvider.System,
            new OptionsWrapper<OutboxProcessorOptions>(opts),
            Mock.Of<ILogger<OutboxProcessor>>());
    }

    private static OutboxMessage MakeMessage(string type = "incident.created", string payload = "{}")
    {
        // OutboxMessage.Create(string type, string payload, Guid? tenantId = null)
        return OutboxMessage.Create(type, payload);
    }

    // ── no pending messages ──────────────────────────────────────────────────

    [Fact]
    public async Task DispatchPendingAsync_WithEmptyOutbox_ReturnsZero()
    {
        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        var publisher = Mock.Of<IOutboxPublisher>();
        var processor = BuildProcessor(outbox.Object, publisher);

        var dispatched = await processor.DispatchPendingAsync();

        Assert.Equal(0, dispatched);
    }

    [Fact]
    public async Task DispatchPendingAsync_WithEmptyOutbox_NeverCallsPublisher()
    {
        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        var publisher = new Mock<IOutboxPublisher>();
        var processor = BuildProcessor(outbox.Object, publisher.Object);

        await processor.DispatchPendingAsync();

        publisher.Verify(
            p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── single message published successfully ────────────────────────────────

    [Fact]
    public async Task DispatchPendingAsync_WithOnePendingMessage_ReturnsOne()
    {
        var message = MakeMessage();
        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([message]);

        var publisher = new Mock<IOutboxPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var processor = BuildProcessor(outbox.Object, publisher.Object);

        var dispatched = await processor.DispatchPendingAsync();

        Assert.Equal(1, dispatched);
    }

    [Fact]
    public async Task DispatchPendingAsync_PublishesWithCorrectTypeAndPayload()
    {
        const string eventType = "sla.breached";
        const string payload = """{"incidentId":"abc"}""";
        var message = MakeMessage(eventType, payload);

        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([message]);

        var publisher = new Mock<IOutboxPublisher>();
        var processor = BuildProcessor(outbox.Object, publisher.Object);

        await processor.DispatchPendingAsync();

        publisher.Verify(
            p => p.PublishAsync(eventType, payload, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchPendingAsync_AfterSuccess_MarksMessageProcessed()
    {
        var message = MakeMessage();
        Assert.Equal(OutboxMessageState.Pending, message.State);

        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([message]);

        var publisher = new Mock<IOutboxPublisher>();
        var processor = BuildProcessor(outbox.Object, publisher.Object);

        await processor.DispatchPendingAsync();

        Assert.Equal(OutboxMessageState.Processed, message.State);
    }

    [Fact]
    public async Task DispatchPendingAsync_AfterSuccess_SavesChanges()
    {
        var message = MakeMessage();
        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([message]);

        var processor = BuildProcessor(outbox.Object, Mock.Of<IOutboxPublisher>());

        await processor.DispatchPendingAsync();

        outbox.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── publisher failure: retry bookkeeping ─────────────────────────────────

    [Fact]
    public async Task DispatchPendingAsync_WhenPublisherThrows_ReturnsZero()
    {
        var message = MakeMessage();
        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([message]);

        var publisher = new Mock<IOutboxPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("broker is down"));

        var processor = BuildProcessor(outbox.Object, publisher.Object);

        var dispatched = await processor.DispatchPendingAsync();

        Assert.Equal(0, dispatched);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenPublisherThrows_IncrementsAttemptCount()
    {
        var message = MakeMessage();
        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([message]);

        var publisher = new Mock<IOutboxPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new Exception("timeout"));

        var processor = BuildProcessor(outbox.Object, publisher.Object);

        await processor.DispatchPendingAsync();

        Assert.Equal(1, message.Attempts);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenPublisherThrowsOnFinalAttempt_MarksMessageFailed()
    {
        // MaxAttempts = 1 means the first failure permanently parks the message.
        var options = new OutboxProcessorOptions { Enabled = true, BatchSize = 50, MaxAttempts = 1 };
        var message = MakeMessage();

        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([message]);

        var publisher = new Mock<IOutboxPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new Exception("broker offline"));

        var processor = BuildProcessor(outbox.Object, publisher.Object, options);

        await processor.DispatchPendingAsync();

        Assert.Equal(OutboxMessageState.Failed, message.State);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenPublisherThrowsBeforeFinalAttempt_LeavesMessagePending()
    {
        // MaxAttempts = 3, first failure → still Pending (not Failed).
        var options = new OutboxProcessorOptions { Enabled = true, BatchSize = 50, MaxAttempts = 3 };
        var message = MakeMessage();

        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([message]);

        var publisher = new Mock<IOutboxPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new Exception("transient"));

        var processor = BuildProcessor(outbox.Object, publisher.Object, options);

        await processor.DispatchPendingAsync();

        Assert.NotEqual(OutboxMessageState.Failed, message.State);
    }

    // ── batch: one failure does not stop the rest ────────────────────────────

    [Fact]
    public async Task DispatchPendingAsync_WhenFirstMessageFails_StillPublishesSecond()
    {
        var first = MakeMessage("incident.created");
        var second = MakeMessage("incident.resolved");

        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([first, second]);

        var publisher = new Mock<IOutboxPublisher>();
        publisher
            .SetupSequence(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("first fails"))
            .Returns(Task.CompletedTask);

        var processor = BuildProcessor(outbox.Object, publisher.Object);

        var dispatched = await processor.DispatchPendingAsync();

        Assert.Equal(1, dispatched);
        Assert.Equal(OutboxMessageState.Processed, second.State);
    }

    // ── batch size ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchPendingAsync_RequestsConfiguredBatchSize()
    {
        var opts = new OutboxProcessorOptions { BatchSize = 17 };
        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        var processor = BuildProcessor(outbox.Object, Mock.Of<IOutboxPublisher>(), opts);

        await processor.DispatchPendingAsync();

        outbox.Verify(r => r.GetPendingAsync(17, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── cancellation propagation ─────────────────────────────────────────────

    [Fact]
    public async Task DispatchPendingAsync_WhenCancelledAndPublisherThrowsOce_Rethrows()
    {
        using var cts = new CancellationTokenSource();

        var message = MakeMessage();
        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([message]);

        var publisher = new Mock<IOutboxPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns<string, string, CancellationToken>((_, _, ct) =>
                 {
                     cts.Cancel();
                     ct.ThrowIfCancellationRequested();
                     return Task.CompletedTask;
                 });

        var processor = BuildProcessor(outbox.Object, publisher.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => processor.DispatchPendingAsync(cts.Token));
    }

    // ── scope disposal ───────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchPendingAsync_DisposesAnAsyncOnlyScopedPublisher()
    {
        // The real publisher owns an AMQP channel and implements only IAsyncDisposable. The
        // pass must therefore give its scope back asynchronously; a synchronous dispose throws
        // "type only implements IAsyncDisposable" and fails every drain.
        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        var services = new ServiceCollection();
        services.AddScoped(_ => outbox.Object);
        services.AddScoped<IOutboxPublisher, AsyncOnlyPublisher>();

        await using var provider = services.BuildServiceProvider();

        var processor = new OutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            new OptionsWrapper<OutboxProcessorOptions>(
                new OutboxProcessorOptions { Enabled = true, BatchSize = 50, MaxAttempts = 5 }),
            Mock.Of<ILogger<OutboxProcessor>>());

        var dispatched = await processor.DispatchPendingAsync();

        Assert.Equal(0, dispatched);
    }

    /// <summary>A publisher shaped like the real one: async disposal only.</summary>
    private sealed class AsyncOnlyPublisher : IOutboxPublisher, IAsyncDisposable
    {
        public Task PublishAsync(
            string type,
            string payload,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
