namespace OpsPilot.Contracts.Incidents;

/// <summary>Opens an incident by hand. The tenant and reporter come from the bearer token.</summary>
public sealed record CreateIncidentRequest(
    Guid ServiceId,
    string Title,
    string Description,
    string Severity,
    string? CorrelationKey = null);

/// <summary>Edits the mutable fields of an incident.</summary>
public sealed record UpdateIncidentRequest(
    string Title,
    string Description,
    string Severity);

public sealed record AssignIncidentRequest(Guid AssigneeId);

public sealed record AddIncidentCommentRequest(string Body);

/// <summary>Requests a state change. The permission required depends on <see cref="ToStatus"/>.</summary>
public sealed record TransitionIncidentRequest(string ToStatus, string? Note = null);
