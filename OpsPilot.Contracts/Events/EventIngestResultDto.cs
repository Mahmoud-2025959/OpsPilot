namespace OpsPilot.Contracts.Events;

/// <summary>
/// The acknowledgement for an ingested event (plan, UC-06, answered with 202 Accepted).
/// </summary>
/// <param name="EventId">The timeline entry the event became.</param>
/// <param name="IncidentId">
/// The incident the event opened or, when <paramref name="Correlated"/> is true, the open one it
/// joined.
/// </param>
/// <param name="Correlated">
/// True when the event matched an already-open incident, false when it opened a new one.
/// </param>
public sealed record EventIngestResultDto(Guid EventId, Guid IncidentId, bool Correlated);
