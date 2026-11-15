namespace OpsPilot.Contracts.Events;

/// <summary>
/// An operational event pushed by one of the company's own applications or monitoring tools
/// (plan, UC-06). The owning tenant is taken from the API key, never from the body, and the
/// service is identified by name plus environment rather than by id.
/// </summary>
/// <param name="Timestamp">
/// When the event happened, if the sender knows. Defaults to the moment the API receives it.
/// </param>
public sealed record IngestEventRequest(
    string Service,
    string Environment,
    string Severity,
    string Message,
    DateTimeOffset? Timestamp = null);
