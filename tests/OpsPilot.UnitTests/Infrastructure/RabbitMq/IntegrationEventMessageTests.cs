using System.Text.Json;
using OpsPilot.Application.Common.Messages;

namespace OpsPilot.UnitTests.Infrastructure.RabbitMq;

/// <summary>
/// Tests for <see cref="IntegrationEventMessage.TryParse"/> — the envelope the consumer reads.
/// </summary>
public class IntegrationEventMessageTests
{
    private static readonly Guid SomeTenant = Guid.NewGuid();

    private static string ValidJson(Guid? tenantId = null, string type = "incident.created", string payload = "{}")
    {
        var id = (tenantId ?? SomeTenant).ToString();
        return $$"""{"tenantId":"{{id}}","type":"{{type}}","payload":{{payload}}}""";
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_WithValidJson_ReturnsTrue()
    {
        var result = IntegrationEventMessage.TryParse(ValidJson(), out var message);

        Assert.True(result);
        Assert.NotNull(message);
    }

    [Fact]
    public void TryParse_WithValidJson_PopulatesTenantId()
    {
        IntegrationEventMessage.TryParse(ValidJson(SomeTenant), out var message);
        Assert.Equal(SomeTenant, message!.TenantId);
    }

    [Fact]
    public void TryParse_WithValidJson_PopulatesType()
    {
        IntegrationEventMessage.TryParse(ValidJson(type: "sla.breached"), out var message);
        Assert.Equal("sla.breached", message!.Type);
    }

    [Fact]
    public void TryParse_WithValidJson_PopulatesPayload()
    {
        var json = ValidJson(payload: """{"incidentId":"abc"}""");
        IntegrationEventMessage.TryParse(json, out var message);
        Assert.Equal(JsonValueKind.Object, message!.Payload.ValueKind);
    }

    // ── guard: null / empty body ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_WithNullOrWhitespace_ReturnsFalse(string? body)
    {
        var result = IntegrationEventMessage.TryParse(body, out var message);

        Assert.False(result);
        Assert.Null(message);
    }

    // ── guard: malformed JSON ────────────────────────────────────────────────

    [Theory]
    [InlineData("not json")]
    [InlineData("{broken")]
    [InlineData("[]")]
    public void TryParse_WithMalformedJson_ReturnsFalse(string body)
    {
        var result = IntegrationEventMessage.TryParse(body, out var message);

        Assert.False(result);
        Assert.Null(message);
    }

    // ── guard: missing / invalid fields ─────────────────────────────────────

    [Fact]
    public void TryParse_WithEmptyTenantId_ReturnsFalse()
    {
        // Guid.Empty means no tenant context — cannot be applied safely.
        var json = ValidJson(tenantId: Guid.Empty);
        var result = IntegrationEventMessage.TryParse(json, out _);
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_WithBlankType_ReturnsFalse(string type)
    {
        // A blank type has no handler: the envelope is malformed, not just unknown.
        var json = ValidJson(type: type);
        var result = IntegrationEventMessage.TryParse(json, out _);
        Assert.False(result);
    }

    // ── routing key formats used in the real system ──────────────────────────

    [Theory]
    [InlineData("incident.created")]
    [InlineData("incident.resolved")]
    [InlineData("sla.breached")]
    [InlineData("webhook.delivered")]
    public void TryParse_AcceptsAllKnownEventTypes(string type)
    {
        var result = IntegrationEventMessage.TryParse(ValidJson(type: type), out var message);

        Assert.True(result);
        Assert.Equal(type, message!.Type);
    }
}
