using System.Text.Json;
using OpsPilot.Application.Common.Json;

namespace OpsPilot.Application.Common.Messages;

/// <summary>
/// An integration event that arrived over the broker (plan, UC-06 step 7).
/// </summary>
/// <param name="TenantId">
/// The company the event belongs to. It travels in the envelope because a consumer has no
/// request to inherit a tenant from, and the company has to be established before anything
/// tenant-scoped is read.
/// </param>
/// <param name="Type">The kind of event. The handler registry is keyed by it.</param>
/// <param name="Payload">The body of the event, exactly as it was published.</param>
public sealed record IntegrationEventMessage(Guid TenantId, string Type, JsonElement Payload)
{
    /// <summary>
    /// Reads an event out of a raw broker message.
    /// </summary>
    /// <remarks>
    /// A message that cannot be read is not an exception: it is a message somebody got wrong,
    /// and the only sensible answer is to refuse it and let the broker park it. Throwing here
    /// would put the decision in the wrong place — the caller has to know whether to retry.
    /// </remarks>
    public static bool TryParse(string? body, out IntegrationEventMessage? message)
    {
        message = null;

        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        IntegrationEventMessage? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<IntegrationEventMessage>(body, JsonDefaults.Options);
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed is null
            || parsed.TenantId == Guid.Empty
            || string.IsNullOrWhiteSpace(parsed.Type))
        {
            return false;
        }

        message = parsed;
        return true;
    }
}
