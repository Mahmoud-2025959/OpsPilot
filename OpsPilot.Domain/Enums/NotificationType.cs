namespace OpsPilot.Domain.Enums;

public enum NotificationType
{
    IncidentCreated,
    IncidentAssigned,
    IncidentStatusChanged,
    IncidentResolved,
    IncidentClosed,
    SlaViolation,
    SlaBreachCritical,
    PostmortemPublished,
    DailyDigest
}