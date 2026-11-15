namespace OpsPilot.Contracts.Postmortems;

/// <summary>
/// Opens the single postmortem an incident may have. The incident comes from the route, so it
/// is not part of the body.
/// </summary>
/// <param name="Title">A short headline for the retrospective.</param>
/// <param name="Summary">What happened, in narrative form.</param>
/// <param name="RootCause">Why it happened, if it is already known.</param>
/// <param name="ActionItems">The follow-up work agreed, if any.</param>
public sealed record CreatePostmortemRequest(
    string Title,
    string Summary,
    string? RootCause = null,
    string? ActionItems = null);

/// <summary>Replaces the whole written body of a draft postmortem.</summary>
/// <param name="Title">A short headline for the retrospective.</param>
/// <param name="Summary">What happened, in narrative form.</param>
/// <param name="RootCause">Why it happened, or null to clear it.</param>
/// <param name="ActionItems">The follow-up work agreed, or null to clear it.</param>
public sealed record UpdatePostmortemRequest(
    string Title,
    string Summary,
    string? RootCause = null,
    string? ActionItems = null);
