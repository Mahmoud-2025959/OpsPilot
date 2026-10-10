using OpsPilot.Contracts.Incidents;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class IncidentMappings
{
    public static IncidentDto ToDto(this Incident i) =>
        new(i.Id,
            i.TenantId,
            i.ServiceId,
            i.Title,
            i.Description,
            i.Severity.ToString(),
            i.Status.ToString(),
            i.CreatedByUserId,
            i.AssignedTo,
            i.CorrelationKey,
            i.OccurrenceCount,
            i.AcknowledgedAt,
            i.InvestigatingAt,
            i.MitigatedAt,
            i.ResolvedAt,
            i.ClosedAt,
            i.ResponseDueAt,
            i.ResolutionDueAt,
            i.CreatedAt,
            i.UpdatedAt);

    public static IncidentCommentDto ToDto(this IncidentComment c) =>
        new(c.Id, c.IncidentId, c.AuthorId, c.Body, c.CreatedAt);

    public static IncidentEventDto ToDto(this IncidentEvent e) =>
        new(e.Id, e.IncidentId, e.Type.ToString(), e.ApiKeyId, e.Payload, e.OccurredAt);
}
