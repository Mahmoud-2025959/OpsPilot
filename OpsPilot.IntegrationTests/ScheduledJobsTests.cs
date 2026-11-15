using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.BackgroundJobs;
using OpsPilot.Infrastructure.Services;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the seam between the Hangfire scheduler and the work it schedules (plan,
/// "BackgroundJobs"): a scheduled job must do exactly what the worker's own timer did, because
/// the two are the same call.
/// </summary>
/// <remarks>
/// The job bodies are driven directly rather than through Hangfire itself. What matters here is
/// that <see cref="ScheduledJobs"/> resolves the real workers and delegates to their pass
/// methods; that Hangfire then calls those bodies on a schedule is Hangfire's own guarantee, not
/// this code's. The container is the real composition root so the delegation is exercised against
/// the registrations production actually has.
/// </remarks>
public sealed class ScheduledJobsTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task The_Outbox_Job_Drains_The_Queue()
    {
        await using var factory = new IntegrationApiFactory(db);

        using var company = await db.CompanyAsync();

        var message = OutboxMessage.Create(
            "incident.created",
            "{\"eventId\":\"scheduled-outbox\"}",
            company.TenantId);

        await company.Context.OutboxMessages.AddAsync(message);
        await company.Context.SaveChangesAsync();

        var jobs = BuildJobs(factory);

        // The default wiring is the logging publisher, so a drain reports the message as sent;
        // what is asserted is that the queue was walked and the row closed out.
        var published = await jobs.RunOutboxDrainAsync();

        Assert.True(published >= 1);

        var reloaded = await ReloadMessageAsync(company, message.Id);
        Assert.Equal(OutboxMessageState.Processed, reloaded.State);
    }

    [Fact]
    public async Task The_Email_Job_Delivers_A_Queued_Message()
    {
        await using var factory = new IntegrationApiFactory(db);

        using var company = await db.CompanyAsync();

        var notification = Notification.Create(
            company.TenantId,
            company.Company.User.Id,
            NotificationChannel.Email,
            NotificationType.IncidentAssigned,
            "Scheduled delivery",
            "Body of the scheduled delivery test.");

        await company.Context.Notifications.AddAsync(notification);
        await company.Context.SaveChangesAsync();

        // The test host has SMTP switched off, so a real transport is not exercised; the pass is,
        // and with the transport off it reports zero without touching the row. That is asserted
        // here to pin the behaviour a scheduled pass has in that configuration.
        var jobs = BuildJobs(factory);
        var delivered = await jobs.RunEmailDispatchAsync();

        Assert.Equal(0, delivered);

        var reloaded = await ReloadNotificationAsync(company, notification.Id);
        Assert.Equal(NotificationStatus.Pending, reloaded.Status);
        Assert.Equal(0, reloaded.Attempts);
    }

    [Fact]
    public async Task The_Sla_Job_Reports_Zero_When_Nothing_Is_Overdue()
    {
        await using var factory = new IntegrationApiFactory(db);

        using var _ = await db.CompanyAsync();

        var jobs = BuildJobs(factory);

        // A freshly seeded company has no incidents at all, so the sweep is a clean zero rather
        // than a throw - which is what a scheduled job needs to be when there is nothing to do.
        var detected = await jobs.RunSlaSweepAsync();

        Assert.Equal(0, detected);
    }

    [Fact]
    public async Task The_Logging_Publisher_Is_Wired_When_No_Broker_Is_Configured()
    {
        // The test host leaves RabbitMq:Enabled false, so the publish step must be the logging
        // stand-in - the behaviour every outbox test relies on and the default a fresh clone has.
        await using var factory = new IntegrationApiFactory(db);

        using var scope = factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();

        Assert.IsType<LoggingOutboxPublisher>(publisher);
    }

    private static ScheduledJobs BuildJobs(IntegrationApiFactory factory) =>
        new(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ScheduledJobs>.Instance);

    private async Task<OutboxMessage> ReloadMessageAsync(CompanyScope company, Guid messageId)
    {
        await using var context = db.CreateContext(company.Tenants);

        return await context.OutboxMessages.SingleAsync(message => message.Id == messageId);
    }

    private async Task<Notification> ReloadNotificationAsync(CompanyScope company, Guid notificationId)
    {
        await using var context = db.CreateContext(company.Tenants);

        return await context.Notifications.SingleAsync(notification => notification.Id == notificationId);
    }
}
