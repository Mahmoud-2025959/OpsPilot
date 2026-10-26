namespace OpsPilot.Contracts.Postmortems;

/// <summary>
/// A retrospective written after an incident, with the state it is in (ERD §3).
/// </summary>
/// <param name="Id">The postmortem's id.</param>
/// <param name="TenantId">The company the postmortem belongs to.</param>
/// <param name="IncidentId">The incident it was written for; one postmortem per incident.</param>
/// <param name="Title">A short headline for the retrospective.</param>
/// <param name="Summary">What happened, in narrative form.</param>
/// <param name="RootCause">Why it happened, or null while it has not been established.</param>
/// <param name="ActionItems">The follow-up work agreed, or null while none was agreed.</param>
/// <param name="Status">Draft, InReview or Published.</param>
/// <param name="CreatedByUserId">The user who authored the retrospective.</param>
/// <param name="PublishedAt">When it was published, or null while it has not been.</param>
/// <param name="CreatedAt">When the draft was opened.</param>
/// <param name="UpdatedAt">When it was last written to.</param>
public sealed record PostmortemDto(
    Guid Id,
    Guid TenantId,
    Guid IncidentId,
    string Title,
    string Summary,
    string? RootCause,
    string? ActionItems,
    string Status,
    Guid CreatedByUserId,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
