using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.BackgroundJobs;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the reading half of the outbox (plan, UC-06 step 7): a pass publishes what is queued
/// and never publishes it twice, and a message that cannot be delivered uses its attempts up
/// before it is parked.
/// </summary>
public sealed class OutboxDispatchTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task A_Queued_Message_Is_Published_And_Marked_Processed()
    {
        using var company = await db.CompanyAsync();
        var message = await QueueAsync(company, "{\"eventId\":\"1\",\"incidentId\":\"2\"}");

        var publisher = new RecordingOutboxPublisher();
        await using var provider = BuildContainer(publisher);

        var processor = BuildProcessor(provider);
        var published = await processor.DispatchPendingAsync();

        Assert.True(published >= 1);
        Assert.Contains(publisher.Published, sent => sent.Payload == message.Payload);

        var reloaded = await ReloadAsync(company, message.Id);

        Assert.Equal(OutboxMessageState.Processed, reloaded.State);
        Assert.NotNull(reloaded.ProcessedAt);
        Assert.Null(reloaded.Error);
    }

    [Fact]
    public async Task A_Message_Is_Only_Published_Once()
    {
        using var company = await db.CompanyAsync();
        var message = await QueueAsync(company, "{\"eventId\":\"3\",\"incidentId\":\"4\"}");

        var publisher = new RecordingOutboxPublisher();
        await using var provider = BuildContainer(publisher);

        var processor = BuildProcessor(provider);

        await processor.DispatchPendingAsync();
        var secondPass = await processor.DispatchPendingAsync();

        Assert.Equal(0, secondPass);
        Assert.Single(publisher.Published, sent => sent.Payload == message.Payload);

        var reloaded = await ReloadAsync(company, message.Id);
        Assert.Equal(OutboxMessageState.Processed, reloaded.State);
    }

    [Fact]
    public async Task A_Message_That_Cannot_Be_Delivered_Is_Parked_After_Its_Attempts()
    {
        using var company = await db.CompanyAsync();
        var message = await QueueAsync(company, "{\"eventId\":\"5\",\"incidentId\":\"6\"}");

        var publisher = new FailingOutboxPublisher();
        await using var provider = BuildContainer(publisher);

        var processor = BuildProcessor(provider, maxAttempts: 3);

        await processor.DispatchPendingAsync();
        await processor.DispatchPendingAsync();

        // Two attempts used up and still queued: a single failure is not the end of a message.
        var halfway = await ReloadAsync(company, message.Id);
        Assert.Equal(OutboxMessageState.Pending, halfway.State);
        Assert.Equal(2, halfway.Attempts);
        Assert.Equal("The broker is unreachable.", halfway.Error);

        await processor.DispatchPendingAsync();

        var parked = await ReloadAsync(company, message.Id);
        Assert.Equal(OutboxMessageState.Failed, parked.State);
        Assert.Equal(3, parked.Attempts);

        // A parked message is left alone: it is not handed to the publisher again.
        var attemptsBefore = publisher.Attempts;
        await processor.DispatchPendingAsync();

        Assert.Equal(attemptsBefore, publisher.Attempts);
        Assert.Equal(
            OutboxMessageState.Failed,
            (await ReloadAsync(company, message.Id)).State);
    }

    private static async Task<OutboxMessage> QueueAsync(CompanyScope company, string payload)
    {
        var message = OutboxMessage.Create(
            IntegrationEventTypes.IncidentCreated,
            payload,
            company.TenantId);

        await company.Context.OutboxMessages.AddAsync(message);
        await company.Context.SaveChangesAsync();

        return message;
    }

    /// <summary>Reads the row back through a context of its own, so nothing is served from the cache.</summary>
    private async Task<OutboxMessage> ReloadAsync(CompanyScope company, Guid messageId)
    {
        await using var context = db.CreateContext(company.Tenants);

        return await context.OutboxMessages.SingleAsync(message => message.Id == messageId);
    }

    private ServiceProvider BuildContainer(IOutboxPublisher publisher) =>
        new ServiceCollection()
            .AddScoped<TenantContext>()
            .AddScoped<ICurrentTenant>(provider => provider.GetRequiredService<TenantContext>())
            .AddScoped(provider => db.CreateContext(provider.GetRequiredService<TenantContext>()))
            .AddScoped<IOutboxRepository, OutboxRepository>()
            .AddSingleton(publisher)
            .BuildServiceProvider();

    private static OutboxProcessor BuildProcessor(IServiceProvider provider, int maxAttempts = 5) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            Options.Create(new OutboxProcessorOptions
            {
                BatchSize = 10,
                MaxAttempts = maxAttempts
            }),
            NullLogger<OutboxProcessor>.Instance);
}
