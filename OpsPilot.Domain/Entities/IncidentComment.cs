using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Entities;

public sealed class IncidentComment : TenantOwnedEntity
{
    private IncidentComment(Guid incidentId, Guid authorId, string body, DateTimeOffset createdAt)
    {
        IncidentId = incidentId;
        AuthorId = authorId;
        Body = body;
        CreatedAt = createdAt;
    }

    public Guid IncidentId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Body { get; private set; }

    public static IncidentComment Create(
        Guid tenantId,
        Guid incidentId,
        Guid authorId,
        string body,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new IncidentComment(incidentId, authorId, body.Trim(), createdAt)
        {
            TenantId = tenantId
        };
    }
}
