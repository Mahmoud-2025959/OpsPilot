namespace OpsPilot.Domain.Enums;

public enum NotificationType
{
    IncidentCreated,
    IncidentAssigned,
    IncidentStatusChanged,
    IncidentResolved,
    IncidentClosed,
    SlaViolation,
    SlaViolationEscalated,
    SlaBreachCritical,
    PostmortemPublished,
    DailyDigest
}