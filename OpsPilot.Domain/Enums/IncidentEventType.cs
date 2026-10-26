namespace OpsPilot.Domain.Enums;

public enum IncidentEventType
{
    Created,
    Assigned,
    Acknowledged,
    InvestigationStarted,
    Mitigated,
    Resolved,
    Closed,
    Reopened,
    OccurrenceRegistered,
    SlaDeadlineSet,
    SlaViolationDetected,
    CommentAdded,
    PostmortemCreated,
    EventIngested
}