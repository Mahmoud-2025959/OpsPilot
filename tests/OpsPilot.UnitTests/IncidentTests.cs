using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;
using Xunit;

namespace OpsPilot.UnitTests;

public class IncidentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_WithValidData_SetsDefaults()
    {
        var incident = Incident.Create(TenantId, ServiceId, "PaymentService", "DB timeout", Severity.Critical, CreatedBy, Now);

        Assert.Equal(TenantId, incident.TenantId);
        Assert.Equal(ServiceId, incident.ServiceId);
        Assert.Equal("PaymentService", incident.Title);
        Assert.Equal(Severity.Critical, incident.Severity);
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal(1, incident.OccurrenceCount);
        Assert.Equal(Now, incident.CreatedAt);
        Assert.NotEqual(Guid.Empty, incident.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankTitle_Throws(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            Incident.Create(TenantId, ServiceId, title, "DB timeout", Severity.Medium, CreatedBy, Now));
    }

    [Fact]
    public void Create_WithNullTitle_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Incident.Create(TenantId, ServiceId, null!, "DB timeout", Severity.Medium, CreatedBy, Now));
    }

    [Fact]
    public void Assign_SetsAssignee()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.Low, CreatedBy, Now);
        var assignee = Guid.NewGuid();

        incident.Assign(assignee);

        Assert.Equal(assignee, incident.AssignedTo);
    }

    [Fact]
    public void Acknowledge_FromOpen_TransitionsAndSetsTimestamp()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.High, CreatedBy, Now);

        incident.Acknowledge(Now.AddMinutes(1));

        Assert.Equal(IncidentStatus.Acknowledged, incident.Status);
        Assert.Equal(Now.AddMinutes(1), incident.AcknowledgedAt);
    }

    [Fact]
    public void Acknowledge_Twice_Throws()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.High, CreatedBy, Now);
        incident.Acknowledge(Now);

        var error = Assert.Throws<InvalidStateTransitionException>(() => incident.Acknowledge(Now.AddMinutes(1)));

        Assert.Equal(IncidentStatus.Acknowledged, error.From);
        Assert.Equal(IncidentStatus.Acknowledged, error.To);
    }

    [Fact]
    public void FullHappyPath_ReachesClosed()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.Medium, CreatedBy, Now);

        incident.Acknowledge(Now.AddMinutes(1));
        incident.StartInvestigating(Now.AddMinutes(2));
        incident.Mitigate(Now.AddMinutes(3));
        incident.Resolve(Now.AddMinutes(5));
        incident.Close(Now.AddMinutes(6));

        Assert.Equal(IncidentStatus.Closed, incident.Status);
        Assert.NotNull(incident.AcknowledgedAt);
        Assert.NotNull(incident.InvestigatingAt);
        Assert.NotNull(incident.MitigatedAt);
        Assert.NotNull(incident.ResolvedAt);
        Assert.NotNull(incident.ClosedAt);
    }

    [Fact]
    public void SkipAcknowledge_ToInvestigating_Throws()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.High, CreatedBy, Now);

        var error = Assert.Throws<InvalidStateTransitionException>(() => incident.StartInvestigating(Now));

        Assert.Equal(IncidentStatus.Open, error.From);
        Assert.Equal(IncidentStatus.Investigating, error.To);
        Assert.Equal(IncidentStatus.Open, incident.Status);
    }

    [Fact]
    public void Resolve_ThenClose_SetsTimestamps()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.High, CreatedBy, Now);
        incident.Acknowledge(Now);
        incident.StartInvestigating(Now);
        incident.Resolve(Now.AddMinutes(30));

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal(Now.AddMinutes(30), incident.ResolvedAt);

        incident.Close(Now.AddHours(1));

        Assert.Equal(IncidentStatus.Closed, incident.Status);
        Assert.Equal(Now.AddHours(1), incident.ClosedAt);
    }

    [Fact]
    public void Close_FromOpen_Throws()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.Low, CreatedBy, Now);

        var error = Assert.Throws<InvalidStateTransitionException>(() => incident.Close(Now));

        Assert.Equal(IncidentStatus.Open, error.From);
        Assert.Equal(IncidentStatus.Closed, error.To);
    }

    [Fact]
    public void Reopen_FromResolved_TransitionsBackToInvestigating()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.High, CreatedBy, Now);
        incident.Acknowledge(Now);
        incident.StartInvestigating(Now);
        incident.Resolve(Now.AddMinutes(5));

        incident.Reopen(Now.AddHours(1));

        Assert.Equal(IncidentStatus.Investigating, incident.Status);
        Assert.Null(incident.ResolvedAt);
        Assert.Equal(Now.AddHours(1), incident.InvestigatingAt);
    }

    [Fact]
    public void Reopen_FromOpen_Throws()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.Low, CreatedBy, Now);

        // Reopen has a narrower rule than the other transitions, but it must still report the
        // same exception type so callers have one thing to catch.
        var error = Assert.Throws<InvalidStateTransitionException>(() => incident.Reopen(Now));

        Assert.Equal(IncidentStatus.Open, error.From);
        Assert.Equal(IncidentStatus.Investigating, error.To);
    }

    [Fact]
    public void RegisterOccurrence_IncrementsCount()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.Critical, CreatedBy, Now);

        incident.RegisterOccurrence();
        incident.RegisterOccurrence();

        Assert.Equal(3, incident.OccurrenceCount);
    }

    [Fact]
    public void ApplySlaPolicy_ComputesDueDatesAndLinksPolicy()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.Critical, CreatedBy, Now);

        var policyId = Guid.NewGuid();
        incident.ApplySlaPolicy(policyId, TimeSpan.FromMinutes(15), TimeSpan.FromHours(4));

        Assert.Equal(policyId, incident.SlaPolicyId);
        Assert.Equal(Now.AddMinutes(15), incident.ResponseDueAt);
        Assert.Equal(Now.AddHours(4), incident.ResolutionDueAt);
    }

    [Fact]
    public void ApplySlaPolicy_WithResolutionBeforeResponse_Throws()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.Critical, CreatedBy, Now);

        Assert.Throws<DomainException>(
            () => incident.ApplySlaPolicy(Guid.NewGuid(), TimeSpan.FromHours(4), TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void RecordEvent_BumpsOccurrenceCountAndMovesLastEventAt()
    {
        var incident = Incident.Create(TenantId, ServiceId, "Title", "Desc", Severity.Critical, CreatedBy, Now);

        var later = Now.AddMinutes(5);
        incident.RecordEvent(later);

        Assert.Equal(2, incident.OccurrenceCount);
        Assert.Equal(later, incident.LastEventAt);
    }
}