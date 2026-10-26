using System.Text.Json;
using OpsPilot.Application.Common.Json;

namespace OpsPilot.Application.Features.Webhooks;

/// <summary>
/// The body a webhook call carries.
/// </summary>
/// <remarks>
/// The body is an envelope around the event's own payload, so a receiver can tell what
/// happened and to which company without knowing the shape of every event type. The event's
/// payload is copied in as it was published rather than reshaped, which keeps the webhook and
/// the broker saying the same thing about the same event.
/// </remarks>
public static class WebhookPayloads
{
    /// <summary>
    /// Wraps an event payload in the envelope that goes on the wire.
    /// </summary>
    /// <param name="eventType">The kind of event being announced.</param>
    /// <param name="tenantId">The company the event is about.</param>
    /// <param name="occurredAt">When the event happened.</param>
    /// <param name="data">The event's own payload, as JSON.</param>
    public static string Envelope(
        string eventType,
        Guid tenantId,
        DateTimeOffset occurredAt,
        string data)
    {
        object payload;

        try
        {
            // Parsed and cloned rather than pasted in as a string: a payload that is inserted
            // as text would arrive as an escaped blob instead of as the object it is.
            using var document = JsonDocument.Parse(data);
            payload = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not JSON after all. It is still worth delivering — the receiver sees it as a
            // string rather than as a broken body.
            payload = data;
        }

        return JsonSerializer.Serialize(
            new
            {
                @event = eventType,
                tenantId,
                occurredAt,
                data = payload
            },
            JsonDefaults.Options);
    }
}
