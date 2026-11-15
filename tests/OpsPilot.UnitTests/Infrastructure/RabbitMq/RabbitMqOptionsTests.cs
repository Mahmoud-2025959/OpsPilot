using Microsoft.Extensions.Options;
using Moq;
using OpsPilot.Infrastructure.RabbitMQ;

namespace OpsPilot.UnitTests.Infrastructure.RabbitMq;

/// <summary>
/// Tests for <see cref="RabbitMqOptions"/> defaults and binding.
/// </summary>
public class RabbitMqOptionsTests
{
    // ── defaults ────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultOptions_AreDisabled()
    {
        // A fresh clone must never require a running broker.
        var options = new RabbitMqOptions();

        Assert.False(options.Enabled);
        Assert.False(options.ConsumerEnabled);
    }

    [Fact]
    public void DefaultOptions_HostName_IsLocalhost()
    {
        var options = new RabbitMqOptions();
        Assert.Equal("localhost", options.HostName);
    }

    [Fact]
    public void DefaultOptions_Port_IsStandardAmqp()
    {
        var options = new RabbitMqOptions();
        Assert.Equal(5672, options.Port);
    }

    [Fact]
    public void DefaultOptions_VirtualHost_IsRoot()
    {
        var options = new RabbitMqOptions();
        Assert.Equal("/", options.VirtualHost);
    }

    [Fact]
    public void DefaultOptions_UserName_IsGuest()
    {
        var options = new RabbitMqOptions();
        Assert.Equal("guest", options.UserName);
    }

    [Fact]
    public void DefaultOptions_Password_IsEmpty()
    {
        var options = new RabbitMqOptions();
        Assert.Equal(string.Empty, options.Password);
    }

    [Fact]
    public void DefaultOptions_UseTls_IsFalse()
    {
        var options = new RabbitMqOptions();
        Assert.False(options.UseTls);
    }

    [Fact]
    public void DefaultOptions_ExchangeName_MatchesKnownTopology()
    {
        var options = new RabbitMqOptions();
        Assert.Equal("opspilot.events", options.ExchangeName);
    }

    [Fact]
    public void DefaultOptions_DeadLetterExchangeName_MatchesKnownTopology()
    {
        var options = new RabbitMqOptions();
        Assert.Equal("opspilot.events.dlx", options.DeadLetterExchangeName);
    }

    [Fact]
    public void DefaultOptions_DeadLetterQueueName_MatchesKnownTopology()
    {
        var options = new RabbitMqOptions();
        Assert.Equal("opspilot.events.dead", options.DeadLetterQueueName);
    }

    [Fact]
    public void DefaultOptions_ConsumerQueueName_MatchesKnownTopology()
    {
        var options = new RabbitMqOptions();
        Assert.Equal("opspilot.integration", options.ConsumerQueueName);
    }

    [Fact]
    public void DefaultOptions_ConsumerPrefetch_IsLow()
    {
        // A wide window would let one slow tenant hold the whole queue.
        var options = new RabbitMqOptions();
        Assert.Equal((ushort)10, options.ConsumerPrefetch);
    }

    [Fact]
    public void DefaultOptions_PublishTimeout_IsTenSeconds()
    {
        var options = new RabbitMqOptions();
        Assert.Equal(TimeSpan.FromSeconds(10), options.PublishTimeout);
    }

    // ── section name ─────────────────────────────────────────────────────────

    [Fact]
    public void SectionName_Is_RabbitMq()
    {
        // DependencyInjection.cs binds from this constant — it must not change.
        Assert.Equal("RabbitMq", RabbitMqOptions.SectionName);
    }

    // ── IOptions wrapper ─────────────────────────────────────────────────────

    [Fact]
    public void OptionsWrapper_ReturnsConfiguredValues()
    {
        var expected = new RabbitMqOptions
        {
            Enabled = true,
            HostName = "rabbit.example.com",
            Port = 5671,
            UserName = "opspilot",
            Password = "s3cr3t",
            UseTls = true
        };

        var wrapper = new Mock<IOptions<RabbitMqOptions>>();
        wrapper.Setup(o => o.Value).Returns(expected);

        var resolved = wrapper.Object.Value;

        Assert.True(resolved.Enabled);
        Assert.Equal("rabbit.example.com", resolved.HostName);
        Assert.Equal(5671, resolved.Port);
        Assert.True(resolved.UseTls);
    }
}
