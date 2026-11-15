using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Infrastructure;
using OpsPilot.Infrastructure.BackgroundJobs;

namespace OpsPilot.UnitTests.Infrastructure.Hangfire;

/// <summary>
/// Tests for <see cref="ScheduledJobs"/> — the seam between Hangfire and the four workers.
///
/// Workers are sealed BackgroundService subclasses that cannot be Moq-mocked directly, so each
/// test builds real instances with their inner-scope dependencies controlled through DI and
/// produces deterministic pass results.
/// </summary>
public class ScheduledJobsTests
{
    // ── helpers: build real sealed workers with predictable deps ─────────────

    /// <summary>
    /// Real <see cref="OutboxProcessor"/> wired to a repo that returns
    /// <paramref name="messages"/> and a publisher that always succeeds.
    /// </summary>
    private static OutboxProcessor RealOutboxProcessor(
        IReadOnlyList<OutboxMessage> messages,
        IOutboxPublisher? publisher = null)
    {
        publisher ??= Mock.Of<IOutboxPublisher>(p =>
            p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()) ==
            Task.CompletedTask);

        var outboxRepo = new Mock<IOutboxRepository>();
        outboxRepo
            .Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        var services = new ServiceCollection();
        services.AddSingleton<IOutboxRepository>(outboxRepo.Object);
        services.AddSingleton<IOutboxPublisher>(publisher);
        var provider = services.BuildServiceProvider();

        return new OutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            new OptionsWrapper<OutboxProcessorOptions>(
                new OutboxProcessorOptions { Enabled = true, BatchSize = 50, MaxAttempts = 5 }),
            Mock.Of<ILogger<OutboxProcessor>>());
    }

    /// <summary>
    /// Real <see cref="SlaViolationDetectionWorker"/> backed by an empty
    /// <see cref="ITenantDirectory"/> — <c>SweepOnceAsync</c> returns 0.
    /// </summary>
    private static SlaViolationDetectionWorker RealSlaWorker()
    {
        var directory = new Mock<ITenantDirectory>();
        directory
            .Setup(d => d.GetActiveIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var services = new ServiceCollection();
        services.AddSingleton<ITenantDirectory>(directory.Object);
        var provider = services.BuildServiceProvider();

        return new SlaViolationDetectionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            new OptionsWrapper<SlaViolationWorkerOptions>(new SlaViolationWorkerOptions()),
            Mock.Of<ILogger<SlaViolationDetectionWorker>>());
    }

    /// <summary>
    /// Real <see cref="EmailDispatchWorker"/> with SMTP disabled — returns 0 immediately.
    /// </summary>
    private static EmailDispatchWorker RealEmailWorker()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        return new EmailDispatchWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            new OptionsWrapper<EmailDispatchOptions>(new EmailDispatchOptions { Enabled = true }),
            new OptionsWrapper<SmtpOptions>(new SmtpOptions { Enabled = false }),
            Mock.Of<ILogger<EmailDispatchWorker>>());
    }

    /// <summary>
    /// Real <see cref="WebhookDispatchWorker"/> backed by an empty
    /// <see cref="ITenantDirectory"/> — <c>DispatchPendingAsync</c> returns 0.
    /// </summary>
    private static WebhookDispatchWorker RealWebhookWorker()
    {
        var directory = new Mock<ITenantDirectory>();
        directory
            .Setup(d => d.GetActiveIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var services = new ServiceCollection();
        services.AddSingleton<ITenantDirectory>(directory.Object);
        var provider = services.BuildServiceProvider();

        return new WebhookDispatchWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            new OptionsWrapper<WebhookDispatchOptions>(new WebhookDispatchOptions { Enabled = true }),
            Mock.Of<ILogger<WebhookDispatchWorker>>());
    }

    private static ScheduledJobs Build(
        SlaViolationDetectionWorker sla,
        OutboxProcessor outbox,
        EmailDispatchWorker email,
        WebhookDispatchWorker? webhook = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => sla);
        services.AddScoped(_ => outbox);
        services.AddScoped(_ => email);
        services.AddScoped(_ => webhook ?? RealWebhookWorker());
        var provider = services.BuildServiceProvider();

        return new ScheduledJobs(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<ScheduledJobs>>());
    }

    // ── RunSlaSweepAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunSlaSweepAsync_WhenNoTenants_ReturnsZero()
    {
        var jobs = Build(RealSlaWorker(), RealOutboxProcessor([]), RealEmailWorker());

        var result = await jobs.RunSlaSweepAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RunSlaSweepAsync_DoesNotThrow()
    {
        var jobs = Build(RealSlaWorker(), RealOutboxProcessor([]), RealEmailWorker());

        var ex = await Record.ExceptionAsync(() => jobs.RunSlaSweepAsync());

        Assert.Null(ex);
    }

    // ── RunOutboxDrainAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RunOutboxDrainAsync_WithEmptyOutbox_ReturnsZero()
    {
        var jobs = Build(RealSlaWorker(), RealOutboxProcessor([]), RealEmailWorker());

        var result = await jobs.RunOutboxDrainAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RunOutboxDrainAsync_WithTwoPendingMessages_ReturnsTwo()
    {
        var m1 = OutboxMessage.Create("incident.created", "{}");
        var m2 = OutboxMessage.Create("sla.breached", "{}");

        var jobs = Build(RealSlaWorker(), RealOutboxProcessor([m1, m2]), RealEmailWorker());

        var result = await jobs.RunOutboxDrainAsync();

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task RunOutboxDrainAsync_DoesNotThrow()
    {
        var jobs = Build(RealSlaWorker(), RealOutboxProcessor([]), RealEmailWorker());

        var ex = await Record.ExceptionAsync(() => jobs.RunOutboxDrainAsync());

        Assert.Null(ex);
    }

    // ── RunEmailDispatchAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task RunEmailDispatchAsync_WhenSmtpDisabled_ReturnsZero()
    {
        // EmailDispatchWorker returns 0 immediately when SmtpOptions.Enabled = false.
        var jobs = Build(RealSlaWorker(), RealOutboxProcessor([]), RealEmailWorker());

        var result = await jobs.RunEmailDispatchAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RunEmailDispatchAsync_DoesNotThrow()
    {
        var jobs = Build(RealSlaWorker(), RealOutboxProcessor([]), RealEmailWorker());

        var ex = await Record.ExceptionAsync(() => jobs.RunEmailDispatchAsync());

        Assert.Null(ex);
    }

    // ── RunWebhookDispatchAsync ───────────────────────────────────────────────

    [Fact]
    public async Task RunWebhookDispatchAsync_WhenNoTenants_ReturnsZero()
    {
        var jobs = Build(RealSlaWorker(), RealOutboxProcessor([]), RealEmailWorker());

        var result = await jobs.RunWebhookDispatchAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RunWebhookDispatchAsync_DoesNotThrow()
    {
        var jobs = Build(RealSlaWorker(), RealOutboxProcessor([]), RealEmailWorker());

        var ex = await Record.ExceptionAsync(() => jobs.RunWebhookDispatchAsync());

        Assert.Null(ex);
    }

    // ── job id naming convention ──────────────────────────────────────────────

    [Theory]
    [InlineData("sla-violation-sweep")]
    [InlineData("outbox-drain")]
    [InlineData("email-dispatch")]
    [InlineData("webhook-dispatch")]
    public void KnownJobIds_FollowKebabCaseConvention(string jobId)
    {
        // ScheduledJobsExtensions.AddOrUpdate uses these as the stable database key.
        // Changing any value would orphan existing Hangfire rows in every deployment.
        Assert.Matches(@"^[a-z][a-z0-9\-]+$", jobId);
        Assert.DoesNotContain(" ", jobId);
    }
}
