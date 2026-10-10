using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// The incident aggregate: the central record that ties a service, a reporter, an
/// assignee, an SLA policy, its timeline and its breach history together.
/// </summary>
public sealed class Incident : TenantOwnedEntity
{
    private Incident(
        Guid tenantId,
        Guid serviceId,
        string title,
        string description,
        Severity severity,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        string? correlationKey)
    {
        TenantId = tenantId;
        ServiceId = serviceId;
        Title = title;
        Description = description;
        Severity = severity;
        CreatedByUserId = createdByUserId;
        CorrelationKey = correlationKey;
        CreatedAt = createdAt;
        LastEventAt = createdAt;
        Status = IncidentStatus.Open;
        OccurrenceCount = 1;
    }

    public Guid ServiceId { get; private set; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public Severity Severity { get; private set; }

    public IncidentStatus Status { get; private set; }

    /// <summary>User that opened the incident. Persisted as the <c>CreatedBy</c> column.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>User currently responsible for the incident, when one has been assigned.</summary>
    public Guid? AssignedTo { get; private set; }

    /// <summary>SLA policy governing this incident, attached when the incident is created.</summary>
    public Guid? SlaPolicyId { get; private set; }

    /// <summary>
    /// Groups repeat occurrences of the same underlying fault. The database enforces a
    /// filtered unique index so that only one <em>open</em> incident may hold a given key.
    /// </summary>
    public string? CorrelationKey { get; private set; }

    public int OccurrenceCount { get; private set; }

    /// <summary>When the most recent correlated event was recorded.</summary>
    public DateTimeOffset? LastEventAt { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public DateTimeOffset? InvestigatingAt { get; private set; }

    public DateTimeOffset? MitigatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset? ResponseDueAt { get; private set; }

    public DateTimeOffset? ResolutionDueAt { get; private set; }

    public static Incident Create(
        Guid tenantId,
        Guid serviceId,
        string title,
        string description,
        Severity severity,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        string? correlationKey = null,
        Guid? slaPolicyId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (createdByUserId == Guid.Empty)
        {
            throw new DomainException("An incident must be created by a user.");
        }

        return new Incident(
            tenantId,
            serviceId,
            title.Trim(),
            description.Trim(),
            severity,
            createdByUserId,
            createdAt,
            correlationKey?.Trim())
        {
            SlaPolicyId = slaPolicyId
        };
    }

    public void Assign(Guid assigneeId)
    {
        if (assigneeId == Guid.Empty)
        {
            throw new DomainException("Cannot assign an incident to an empty user id.");
        }

        AssignedTo = assigneeId;
    }

    public void Unassign()
    {
        AssignedTo = null;
    }

    /// <summary>Attaches (or replaces) the SLA policy and recomputes both deadlines.</summary>
    public void ApplySlaPolicy(Guid slaPolicyId, TimeSpan response, TimeSpan resolution)
    {
        if (slaPolicyId == Guid.Empty)
        {
            throw new DomainException("An SLA policy must have a non-empty id.");
        }

        if (resolution <= response)
        {
            throw new DomainException("Resolution SLA must be greater than response SLA.");
        }

        SlaPolicyId = slaPolicyId;
        ResponseDueAt = CreatedAt.Add(response);
        ResolutionDueAt = CreatedAt.Add(resolution);
    }

    /// <summary>
    /// Records a correlated event: bumps the occurrence counter and moves
    /// <see cref="LastEventAt"/> forward.
    /// </summary>
    public void RecordEvent(DateTimeOffset at)
    {
        OccurrenceCount++;
        LastEventAt = at;
    }

    public void Acknowledge(DateTimeOffset at)
    {
        EnsureTransition(IncidentStatus.Acknowledged);
        Status = IncidentStatus.Acknowledged;
        AcknowledgedAt = at;
    }

    public void StartInvestigating(DateTimeOffset at)
    {
        EnsureTransition(IncidentStatus.Investigating);
        Status = IncidentStatus.Investigating;
        InvestigatingAt = at;
    }

    public void Mitigate(DateTimeOffset at)
    {
        EnsureTransition(IncidentStatus.Mitigated);
        Status = IncidentStatus.Mitigated;
        MitigatedAt = at;
    }

    public void Resolve(DateTimeOffset at)
    {
        EnsureTransition(IncidentStatus.Resolved);
        Status = IncidentStatus.Resolved;
        ResolvedAt = at;
    }

    public void Close(DateTimeOffset at)
    {
        EnsureTransition(IncidentStatus.Closed);
        Status = IncidentStatus.Closed;
        ClosedAt = at;
    }

    public void Reopen(DateTimeOffset at)
    {
        // Deliberately not routed through EnsureTransition: reopening is only legal from
        // Resolved, whereas the other transitions follow a wider state machine. The
        // exception type is the same as the rest, so a caller can handle every rejected
        // transition in one place.
        if (Status != IncidentStatus.Resolved)
        {
            throw new InvalidStateTransitionException(Status, IncidentStatus.Investigating);
        }

        Status = IncidentStatus.Investigating;
        InvestigatingAt = at;
        ResolvedAt = null;
    }

    /// <summary>Records a repeat occurrence of the same underlying fault.</summary>
    public void RegisterOccurrence()
    {
        OccurrenceCount++;
    }

    private void EnsureTransition(IncidentStatus target)
    {
        if (!CanTransitionTo(target))
        {
            throw new InvalidStateTransitionException(Status, target);
        }
    }

    private bool CanTransitionTo(IncidentStatus target)
    {
        return Status switch
        {
            IncidentStatus.Open => target == IncidentStatus.Acknowledged,
            IncidentStatus.Acknowledged => target == IncidentStatus.Investigating,
            IncidentStatus.Investigating => target is IncidentStatus.Mitigated or IncidentStatus.Resolved,
            IncidentStatus.Mitigated => target == IncidentStatus.Resolved,
            IncidentStatus.Resolved => target == IncidentStatus.Closed,
            IncidentStatus.Closed => false,
            _ => false
        };
    }
}
