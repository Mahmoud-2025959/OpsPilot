using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Events.Commands.IngestEvent;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the writing half of the outbox (plan, UC-06 step 7): what ingestion leaves queued and
/// what the queued payload says.
/// </summary>
/// <remarks>
/// The message is staged in the same save as the timeline entry it describes, so these tests
/// exercise the pairing by going through the real ingest handler.
/// </remarks>
public sealed class OutboxStagingTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task A_New_Incident_Leaves_An_IncidentCreated_Message()
    {
        using var company = await db.CompanyAsync();
        var apiKey = await AddApiKeyAsync(company);

        var handler = BuildHandler(company);
        var result = await handler.Handle(AnEvent(company, apiKey.Id), default);

        var message = Assert.Single(await company.Context.OutboxMessages.ToListAsync());

        Assert.Equal(IntegrationEventTypes.IncidentCreated, message.Type);
        Assert.Equal(company.TenantId, message.TenantId);
        Assert.Equal(OutboxMessageState.Pending, message.State);
        Assert.Null(message.ProcessedAt);

        // The payload names the incident and the entry that opened it, and nothing else.
        Assert.Contains(result.IncidentId.ToString(), message.Payload, StringComparison.Ordinal);
        Assert.Contains(result.EventId.ToString(), message.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Repeat_Of_The_Same_Fault_Announces_The_New_Timeline_Entry()
    {
        using var company = await db.CompanyAsync();
        var apiKey = await AddApiKeyAsync(company);

        var handler = BuildHandler(company);

        var first = await handler.Handle(AnEvent(company, apiKey.Id), default);
        var repeat = await handler.Handle(AnEvent(company, apiKey.Id), default);

        Assert.True(repeat.Correlated);
        Assert.Equal(first.IncidentId, repeat.IncidentId);

        var messages = await company.Context.OutboxMessages.ToListAsync();

        Assert.Equal(2, messages.Count);

        var created = Assert.Single(
            messages,
            m => m.Type == IntegrationEventTypes.IncidentCreated);

        Assert.Contains(first.EventId.ToString(), created.Payload, StringComparison.Ordinal);

        var added = Assert.Single(
            messages,
            m => m.Type == IntegrationEventTypes.IncidentEventAdded);

        Assert.Contains(repeat.EventId.ToString(), added.Payload, StringComparison.Ordinal);
        Assert.Contains(first.IncidentId.ToString(), added.Payload, StringComparison.Ordinal);
    }

    private static IngestEventCommand AnEvent(CompanyScope company, Guid apiKeyId) =>
        new(
            company.TenantId,
            apiKeyId,
            company.Company.User.Id,
            "Payments",
            EnvironmentType.Production,
            Severity.High,
            "p99 latency above threshold");

    private static IngestEventCommandHandler BuildHandler(CompanyScope company) =>
        new(
            new ServiceRepository(company.Context),
            new IncidentRepository(company.Context, new RecordingIncidentNotifier()),
            new SlaPolicyRepository(company.Context),
            TimeProvider.System);

    private static async Task<ApiKey> AddApiKeyAsync(CompanyScope company)
    {
        var apiKey = ApiKey.Create(
            company.TenantId,
            "Test ingester",
            $"op_{Guid.NewGuid():N}"[..16],
            new string('a', 64),
            DateTimeOffset.UtcNow.AddDays(90));

        await company.Context.ApiKeys.AddAsync(apiKey);
        await company.Context.SaveChangesAsync();

        return apiKey;
    }
}
