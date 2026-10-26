using OpsPilot.Contracts.Postmortems;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class PostmortemMappings
{
    public static PostmortemDto ToDto(this Postmortem p) =>
        new(p.Id,
            p.TenantId,
            p.IncidentId,
            p.Title,
            p.Summary,
            p.RootCause,
            p.ActionItems,
            p.Status.ToString(),
            p.CreatedByUserId,
            p.PublishedAt,
            p.CreatedAt,
            p.UpdatedAt);
}
