namespace OpsPilot.Contracts.Incidents;

/// <summary>
/// One entry on an incident's timeline.
/// </summary>
/// <param name="ApiKeyId">
/// The key that ingested the event, or null when a user produced it (ERD §3).
/// </param>
/// <param name="Payload">Raw event detail, or null when the event carries none.</param>
public sealed record IncidentEventDto(
    Guid Id,
    Guid IncidentId,
    string Type,
    Guid? ApiKeyId,
    string? Payload,
    DateTimeOffset OccurredAt);
