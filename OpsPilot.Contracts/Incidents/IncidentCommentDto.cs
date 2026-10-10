namespace OpsPilot.Contracts.Incidents;

public sealed record IncidentCommentDto(
    Guid Id,
    Guid IncidentId,
    Guid AuthorId,
    string Body,
    DateTimeOffset CreatedAt);
