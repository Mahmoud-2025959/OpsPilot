namespace OpsPilot.Contracts.SlaPolicies;

/// <summary>
/// One severity's response and resolution targets (ERD §3, "SlaPolicy"). Every company has
/// exactly one of these per severity.
/// </summary>
/// <param name="ResponseMinutes">Minutes allowed before the first response is due.</param>
/// <param name="ResolutionMinutes">Minutes allowed before the incident is due to be resolved.</param>
public sealed record SlaPolicyDto(
    Guid Id,
    string Severity,
    int ResponseMinutes,
    int ResolutionMinutes,
    DateTimeOffset? UpdatedAt);
