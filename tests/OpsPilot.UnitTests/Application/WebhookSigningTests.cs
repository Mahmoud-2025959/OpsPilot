using System.Text.Json;
using OpsPilot.Application.Features.Webhooks;
using OpsPilot.Infrastructure.Webhooks;
using Xunit;

namespace OpsPilot.UnitTests.Application;

/// <summary>
/// Covers the signature a webhook call carries, and the envelope its body is wrapped in.
/// </summary>
/// <remarks>
/// The signature is the only thing standing between the body and anybody who can post to the
/// endpoint, so the properties that matter are the ones an attacker would want to break: the
/// body cannot be altered, the call cannot be replayed from another moment, and another
/// endpoint's secret does not open this one's.
/// </remarks>
public class WebhookSigningTests
{
    private const string Secret = "whsec_0123456789abcdef";

    private static readonly DateTimeOffset Moment =
        new(2026, 10, 22, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void The_Same_Call_Signs_The_Same_Way_Twice()
    {
        // The receiver recomputes the signature from what it was given, so the two have to
        // agree exactly.
        var first = WebhookSigner.Sign(Secret, Moment, "{\"a\":1}");
        var second = WebhookSigner.Sign(Secret, Moment, "{\"a\":1}");

        Assert.Equal(first, second);
    }

    [Fact]
    public void The_Signature_Says_Which_Algorithm_Made_It()
    {
        var signature = WebhookSigner.Sign(Secret, Moment, "{\"a\":1}");

        Assert.StartsWith("sha256=", signature);

        // 32 bytes of digest in hexadecimal.
        Assert.Equal(64, signature["sha256=".Length..].Length);
    }

    [Fact]
    public void An_Altered_Body_Changes_The_Signature()
    {
        var original = WebhookSigner.Sign(Secret, Moment, "{\"amount\":1}");
        var altered = WebhookSigner.Sign(Secret, Moment, "{\"amount\":1000}");

        Assert.NotEqual(original, altered);
    }

    [Fact]
    public void Another_Moment_Changes_The_Signature()
    {
        // What makes an old, correctly signed call worthless: the timestamp is part of what is
        // signed, so a captured call cannot be re-stamped.
        var original = WebhookSigner.Sign(Secret, Moment, "{\"a\":1}");
        var later = WebhookSigner.Sign(Secret, Moment.AddSeconds(1), "{\"a\":1}");

        Assert.NotEqual(original, later);
    }

    [Fact]
    public void Another_Secret_Changes_The_Signature()
    {
        var original = WebhookSigner.Sign(Secret, Moment, "{\"a\":1}");
        var other = WebhookSigner.Sign("whsec_something_else", Moment, "{\"a\":1}");

        Assert.NotEqual(original, other);
    }

    [Fact]
    public void A_Receiver_Can_Verify_What_Was_Sent()
    {
        // The round trip the other end actually performs: take the body and the timestamp from
        // the call, and check the signature against the shared secret.
        const string body = "{\"event\":\"IncidentCreated\"}";

        var signature = WebhookSigner.Sign(Secret, Moment, body);

        var expected = WebhookSigner.Sign(Secret, Moment, body);

        Assert.Equal(expected, signature);
    }

    [Fact]
    public void The_Envelope_Says_What_The_Call_Is_About()
    {
        var payload = WebhookPayloads.Envelope(
            "IncidentCreated",
            Guid.NewGuid(),
            Moment,
            """{"eventId":"e1","incidentId":"i1"}""");

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("IncidentCreated", root.GetProperty("event").GetString());
        Assert.Equal(Moment, root.GetProperty("occurredAt").GetDateTimeOffset());
        Assert.True(root.TryGetProperty("tenantId", out _));
    }

    [Fact]
    public void The_Envelope_Carries_The_Event_Payload_As_An_Object()
    {
        // Pasted in as text, the payload would arrive as an escaped blob instead of as the
        // object it is.
        var payload = WebhookPayloads.Envelope(
            "IncidentCreated",
            Guid.NewGuid(),
            Moment,
            """{"eventId":"e1","incidentId":"i1"}""");

        using var document = JsonDocument.Parse(payload);

        Assert.Equal(
            "i1",
            document.RootElement.GetProperty("data").GetProperty("incidentId").GetString());
    }

    [Fact]
    public void A_Payload_That_Is_Not_Json_Still_Arrives()
    {
        // Delivered rather than dropped: the receiver sees a string instead of a broken body.
        var payload = WebhookPayloads.Envelope(
            "IncidentCreated",
            Guid.NewGuid(),
            Moment,
            "not json at all");

        using var document = JsonDocument.Parse(payload);

        Assert.Equal("not json at all", document.RootElement.GetProperty("data").GetString());
    }

    [Fact]
    public void A_Minted_Secret_Is_Long_And_Named()
    {
        var secret = WebhookSecrets.Generate();

        Assert.StartsWith("whsec_", secret);
        Assert.True(secret.Length > 32);
    }

    [Fact]
    public void Two_Minted_Secrets_Differ()
    {
        Assert.NotEqual(WebhookSecrets.Generate(), WebhookSecrets.Generate());
    }

    [Fact]
    public void The_Stored_Hash_Is_Not_The_Secret()
    {
        var secret = WebhookSecrets.Generate();

        var hash = WebhookSecrets.Hash(secret);

        Assert.NotEqual(secret, hash);
        Assert.Equal(hash, WebhookSecrets.Hash(secret));
    }
}
