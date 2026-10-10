using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// The retrospective written after an incident. One-to-one with an incident,
/// enforced by a unique index on <see cref="IncidentId"/>.
/// </summary>
public sealed class Postmortem : TenantOwnedEntity
{
    private Postmortem(
        Guid tenantId,
        Guid incidentId,
        string title,
        string summary,
        Guid createdByUserId,
        DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        IncidentId = incidentId;
        Title = title;
        Summary = summary;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public Guid IncidentId { get; private set; }

    public string Title { get; private set; }

    public string Summary { get; private set; }

    /// <summary>Why the incident actually happened, in free text.</summary>
    public string? RootCause { get; private set; }

    /// <summary>Follow-up work agreed during the retrospective, in free text.</summary>
    public string? ActionItems { get; private set; }

    public PostmortemStatus Status { get; private set; } = PostmortemStatus.Draft;

    /// <summary>User that authored the retrospective. Persisted as the <c>CreatedBy</c> column.</summary>
    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public static Postmortem Create(
        Guid tenantId,
        Guid incidentId,
        string title,
        string summary,
        Guid createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        return new Postmortem(tenantId, incidentId, title.Trim(), summary.Trim(), createdByUserId, DateTimeOffset.UtcNow);
    }

    public void RecordRootCause(string? rootCause)
    {
        RootCause = rootCause?.Trim();
    }

    public void RecordActionItems(string? actionItems)
    {
        ActionItems = actionItems?.Trim();
    }

    public void MoveToReview()
    {
        if (Status is not PostmortemStatus.Draft)
        {
            throw new InvalidStateTransitionException(
                $"Cannot move postmortem from status '{Status}' to '{PostmortemStatus.InReview}'.");
        }

        Status = PostmortemStatus.InReview;
    }

    public void Publish(DateTimeOffset at)
    {
        if (Status is not PostmortemStatus.InReview)
        {
            throw new InvalidStateTransitionException(
                $"Cannot publish postmortem from status '{Status}'.");
        }

        Status = PostmortemStatus.Published;
        PublishedAt = at;
    }
}
