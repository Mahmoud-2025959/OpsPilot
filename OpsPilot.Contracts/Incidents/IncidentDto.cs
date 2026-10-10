namespace OpsPilot.Contracts.Incidents;

public sealed record IncidentDto(
    Guid Id,
    Guid TenantId,
    Guid ServiceId,
    string Title,
    string Description,
    string Severity,
    string Status,
    Guid CreatedById,
    Guid? AssignedTo,
    string? CorrelationKey,
    int OccurrenceCount,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? InvestigatingAt,
    DateTimeOffset? MitigatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? ResponseDueAt,
    DateTimeOffset? ResolutionDueAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
