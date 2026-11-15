using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure;
using OpsPilot.Infrastructure.BackgroundJobs;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the delivery half of the email channel (plan, notifications group): a pass turns a
/// queued row into mail, a transport that refuses earns its retries until the row is parked,
/// and a transport that is switched off leaves the queue exactly as it was.
/// </summary>
/// <remarks>
/// Assertions are row-scoped - matched on this notification's own subject - because every test
/// in the class shares one database and the sweep necessarily walks every company in it.
/// </remarks>
public sealed class EmailDispatchTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task A_Queued_Email_Is_Delivered_And_Marked_Sent()
    {
        using var company = await db.CompanyAsync();
        var notification = await QueueAsync(company, "Queued email is delivered");

        var sender = new RecordingEmailSender();
        await using var provider = BuildContainer(sender);

        var worker = BuildWorker(provider);
        var delivered = await worker.DispatchPendingEmailsAsync();

        Assert.True(delivered >= 1);

        var sent = Assert.Single(sender.Sent, mail => mail.Subject == notification.Title);
        Assert.Equal(company.Company.User.Email, sent.To);
        Assert.Equal(notification.Body, sent.Body);

        var reloaded = await ReloadAsync(company, notification.Id);

        Assert.Equal(NotificationStatus.Sent, reloaded.Status);
        Assert.NotNull(reloaded.SentAt);
        Assert.Null(reloaded.LastError);
        Assert.Equal(0, reloaded.Attempts);
    }

    [Fact]
    public async Task A_Failed_Send_Retries_Until_The_Row_Is_Parked()
    {
        using var company = await db.CompanyAsync();
        var notification = await QueueAsync(company, "Retried then parked");

        var sender = new FailingEmailSender();
        await using var provider = BuildContainer(sender);

        var worker = BuildWorker(provider, maxAttempts: 3);

        await worker.DispatchPendingEmailsAsync();
        await worker.DispatchPendingEmailsAsync();

        // Two attempts used up and still queued: a single failure is not the end of a row.
        var halfway = await ReloadAsync(company, notification.Id);

        Assert.Equal(NotificationStatus.Pending, halfway.Status);
        Assert.Equal(2, halfway.Attempts);
        Assert.Equal("The mail server is unreachable.", halfway.LastError);

        await worker.DispatchPendingEmailsAsync();

        var parked = await ReloadAsync(company, notification.Id);

        Assert.Equal(NotificationStatus.Failed, parked.Status);
        Assert.Equal(3, parked.Attempts);
        Assert.Equal("The mail server is unreachable.", parked.LastError);

        // A parked row stays parked: further passes leave it and its attempt count alone.
        await worker.DispatchPendingEmailsAsync();

        var untouched = await ReloadAsync(company, notification.Id);

        Assert.Equal(NotificationStatus.Failed, untouched.Status);
        Assert.Equal(3, untouched.Attempts);
    }

    [Fact]
    public async Task A_Recipient_Without_A_Deliverable_Address_Is_Parked_Immediately()
    {
        using var company = await db.CompanyAsync();
        var notification = await QueueAsync(company, "Parked without an address");

        // The address is read at send time on purpose, so a recipient who has since
        // disappeared - here a retired account - has nowhere to go and a retry cannot help.
        company.Company.User.SoftDelete();
        company.Context.Users.Update(company.Company.User);
        await company.Context.SaveChangesAsync();

        var sender = new RecordingEmailSender();
        await using var provider = BuildContainer(sender);

        var worker = BuildWorker(provider);
        await worker.DispatchPendingEmailsAsync();

        Assert.DoesNotContain(sender.Sent, mail => mail.Subject == notification.Title);

        var parked = await ReloadAsync(company, notification.Id);

        Assert.Equal(NotificationStatus.Failed, parked.Status);
        Assert.Equal(1, parked.Attempts);
        Assert.Equal("The recipient has no deliverable address.", parked.LastError);
    }

    [Fact]
    public async Task A_Transport_That_Is_Switched_Off_Leaves_The_Queue_Alone()
    {
        using var company = await db.CompanyAsync();
        var notification = await QueueAsync(company, "Waiting for the transport");

        var sender = new RecordingEmailSender();
        await using var provider = BuildContainer(sender);

        var worker = BuildWorker(provider, smtpEnabled: false);
        var delivered = await worker.DispatchPendingEmailsAsync();

        Assert.Equal(0, delivered);
        Assert.Empty(sender.Sent);

        // Attempts included: burning them with no transport around would park rows that never
        // had a fair chance to send, and switching the transport on later must deliver
        // everything that piled up meanwhile.
        var untouched = await ReloadAsync(company, notification.Id);

        Assert.Equal(NotificationStatus.Pending, untouched.Status);
        Assert.Equal(0, untouched.Attempts);
        Assert.Null(untouched.LastError);

        // Tidy the row away: left queued, it would become this class's later tests' delivery
        // rather than remain this test's own subject.
        await company.Context.Notifications
            .Where(n => n.Id == notification.Id)
            .ExecuteDeleteAsync();
    }

    private static async Task<Notification> QueueAsync(CompanyScope company, string title)
    {
        var notification = Notification.Create(
            company.TenantId,
            company.Company.User.Id,
            NotificationChannel.Email,
            NotificationType.IncidentAssigned,
            title,
            $"Body of \"{title}\" - the service is down, please investigate.");

        await company.Context.Notifications.AddAsync(notification);
        await company.Context.SaveChangesAsync();

        return notification;
    }

    /// <summary>Reads the row back through a context of its own, so nothing is served from the cache.</summary>
    private async Task<Notification> ReloadAsync(CompanyScope company, Guid notificationId)
    {
        await using var context = db.CreateContext(company.Tenants);

        return await context.Notifications.SingleAsync(n => n.Id == notificationId);
    }

    private ServiceProvider BuildContainer(IEmailSender sender) =>
        new ServiceCollection()
            .AddScoped<TenantContext>()
            .AddScoped<ICurrentTenant>(provider => provider.GetRequiredService<TenantContext>())
            .AddScoped(provider => db.CreateContext(provider.GetRequiredService<TenantContext>()))
            .AddScoped<INotificationRepository, NotificationRepository>()
            .AddScoped<ITenantDirectory, TenantDirectory>()
            .AddSingleton(sender)
            .BuildServiceProvider();

    private static EmailDispatchWorker BuildWorker(
        IServiceProvider provider,
        int maxAttempts = 5,
        bool smtpEnabled = true) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            Options.Create(new EmailDispatchOptions
            {
                BatchSize = 10,
                MaxAttempts = maxAttempts
            }),
            Options.Create(new SmtpOptions { Enabled = smtpEnabled }),
            NullLogger<EmailDispatchWorker>.Instance);
}
