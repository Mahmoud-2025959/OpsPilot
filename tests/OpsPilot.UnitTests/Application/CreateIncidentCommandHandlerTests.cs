using System.Threading;
using System.Threading.Tasks;
using Moq;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Features.Incidents.Commands;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.UnitTests.Application;

public class CreateIncidentCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsIncidentId()
    {
        var expectedId = Guid.NewGuid();
        var repository = new Mock<IIncidentRepository>();
        repository.Setup(r => r.AddAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);
        var handler = new CreateIncidentCommandHandler(repository.Object);
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
        var handler = new CreateIncidentCommandHandler(repository.Object);
        var command = new CreateIncidentCommand(
            TenantId, ServiceId, "DB timeout", "Connection pool exhausted", Severity.Medium, CreatedBy,
            "corr-123");

        await handler.Handle(command, CancellationToken.None);

        repository.Verify(r => r.AddAsync(It.Is<Incident>(i => i.CorrelationKey == "corr-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}