namespace OpsPilot.Contracts.Incidents;

public sealed record CreateIncidentRequest(
    Guid TenantId,
    Guid ServiceId,
    string Title,
    string Description,
    string Severity,
    string? CorrelationKey = null);

public sealed record AssignIncidentRequest(Guid AssigneeId);

public sealed record AddIncidentCommentRequest(string Body);
