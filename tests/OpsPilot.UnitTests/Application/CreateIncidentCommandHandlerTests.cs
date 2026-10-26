using System.Threading;
using System.Threading.Tasks;
using Moq;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Features.Incidents.Commands;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;
using Xunit;

namespace OpsPilot.UnitTests.Application;

public class CreateIncidentCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static Service KnownService() =>
        Service.Create(TenantId, "Payments", "payments", EnvironmentType.Production);

    private static CreateIncidentCommandHandler HandlerFor(
        Mock<IIncidentRepository> incidents,
        Service? service = null,
        SlaPolicy? policy = null)
    {
        var services = new Mock<IServiceRepository>();
        services.Setup(r => r.GetByIdAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        var slaPolicies = new Mock<ISlaPolicyRepository>();
        slaPolicies.Setup(r => r.GetBySeverityAsync(It.IsAny<Severity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        return new CreateIncidentCommandHandler(incidents.Object, services.Object, slaPolicies.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsIncidentId()
    {
        var expectedId = Guid.NewGuid();
        var repository = new Mock<IIncidentRepository>();
        repository.Setup(r => r.AddAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);
        var handler = HandlerFor(repository, KnownService());
        var command = new CreateIncidentCommand(
            TenantId, ServiceId, "API is down", "5xx on /v1/events", Severity.Critical, CreatedBy);

        var id = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedId, id);
        repository.Verify(r => r.AddAsync(It.Is<Incident>(i =>
            i.Id != Guid.Empty &&
            i.TenantId == TenantId &&
            i.ServiceId == ServiceId &&
            i.Title == "API is down" &&
            i.Severity == Severity.Critical &&
            i.Status == IncidentStatus.Open), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithCorrelationKey_PersistsIncidentWithKey()
    {
        var repository = new Mock<IIncidentRepository>();
        var handler = HandlerFor(repository, KnownService());
        var command = new CreateIncidentCommand(
            TenantId, ServiceId, "DB timeout", "Connection pool exhausted", Severity.Medium, CreatedBy,
            "corr-123");

        await handler.Handle(command, CancellationToken.None);

        repository.Verify(r => r.AddAsync(It.Is<Incident>(i => i.CorrelationKey == "corr-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUnknownService_Throws()
    {
        // The service is a foreign key: without this check the save path answers the caller
        // with a constraint violation dressed up as a 500.
        var repository = new Mock<IIncidentRepository>();
        var handler = HandlerFor(repository, service: null);
        var command = new CreateIncidentCommand(
            TenantId, ServiceId, "API is down", "5xx on /v1/events", Severity.High, CreatedBy);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => handler.Handle(command, CancellationToken.None));

        repository.Verify(
            r => r.AddAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AttachesTheSlaPolicyForTheSeverity()
    {
        var policy = SlaPolicy.Create(TenantId, Severity.Critical, 15, 240);
        var repository = new Mock<IIncidentRepository>();
        var handler = HandlerFor(repository, KnownService(), policy);
        var command = new CreateIncidentCommand(
            TenantId, ServiceId, "API is down", "5xx on /v1/events", Severity.Critical, CreatedBy);

        await handler.Handle(command, CancellationToken.None);

        repository.Verify(r => r.AddAsync(It.Is<Incident>(i =>
            i.SlaPolicyId == policy.Id &&
            i.ResponseDueAt == i.CreatedAt.AddMinutes(15) &&
            i.ResolutionDueAt == i.CreatedAt.AddMinutes(240)), It.IsAny<CancellationToken>()), Times.Once);
    }
}
