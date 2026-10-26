using System.Threading;
using System.Threading.Tasks;
using Moq;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Features.AuditLogs.Queries.GetAuditLogs;
using OpsPilot.Contracts.AuditLogs;
using OpsPilot.Domain.Entities;
using Xunit;

namespace OpsPilot.UnitTests.Application;

/// <summary>
/// Covers reading the trail back: the paging bounds are clamped the same way every other list
/// endpoint clamps them, and a blank filter means "no filter" rather than "match nothing".
/// </summary>
public class GetAuditLogsQueryHandlerTests
{
    private static AuditLog Entry(string action = "Incident.SeverityChanged") =>
        AuditLog.Create(
            Guid.NewGuid(),
            action,
            "Incident",
            Guid.NewGuid().ToString(),
            actorUserId: Guid.NewGuid(),
            oldValues: """{"severity":"Low"}""",
            newValues: """{"severity":"High"}""");

    private static Mock<IAuditLogRepository> RepositoryReturning(
        IReadOnlyList<AuditLog> items,
        int totalCount,
        Action<string?, string?, string?, Guid?, int, int>? capture = null)
    {
        var repository = new Mock<IAuditLogRepository>();

        repository
            .Setup(r => r.GetPagedAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string?, string?, string?, Guid?, DateTimeOffset?, DateTimeOffset?, int, int, CancellationToken>(
                (action, entityType, entityId, actor, _, _, page, pageSize, _) =>
                    capture?.Invoke(action, entityType, entityId, actor, page, pageSize))
            .ReturnsAsync((items, totalCount));

        return repository;
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(-3, 0)]
    [InlineData(1, 20)]
    public async Task Out_Of_Range_Paging_Falls_Back_To_The_Defaults(int page, int pageSize)
    {
        string? seenAction = null, seenEntityType = null, seenEntityId = null;
        Guid? seenActor = null;
        int seenPage = 0, seenPageSize = 0;

        var repository = RepositoryReturning(
            [],
            0,
            (action, entityType, entityId, actor, p, ps) =>
            {
                seenAction = action;
                seenEntityType = entityType;
                seenEntityId = entityId;
                seenActor = actor;
                seenPage = p;
                seenPageSize = ps;
            });

        var handler = new GetAuditLogsQueryHandler(repository.Object);

        await handler.Handle(
            new GetAuditLogsQuery(null, null, null, null, null, null, page, pageSize),
            CancellationToken.None);

        // Out of range falls back to page 1 and the default size of 20; in range passes through.
        Assert.Equal(page < 1 ? 1 : page, seenPage);
        Assert.Equal(pageSize is < 1 or > 100 ? 20 : pageSize, seenPageSize);
        Assert.Null(seenAction);
        Assert.Null(seenEntityType);
        Assert.Null(seenEntityId);
        Assert.Null(seenActor);
    }

    [Fact]
    public async Task Blank_Filters_Are_Dropped_And_Spacing_Is_Trimmed()
    {
        string? seenAction = null, seenEntityType = null, seenEntityId = null;

        var repository = RepositoryReturning(
            [],
            0,
            (action, entityType, entityId, _, _, _) =>
            {
                seenAction = action;
                seenEntityType = entityType;
                seenEntityId = entityId;
            });

        var handler = new GetAuditLogsQueryHandler(repository.Object);

        await handler.Handle(
            new GetAuditLogsQuery("  ", "Incident", "   ", null, null, null),
            CancellationToken.None);

        Assert.Null(seenAction);
        Assert.Equal("Incident", seenEntityType);
        Assert.Null(seenEntityId);
    }

    [Fact]
    public async Task Entries_Are_Mapped_And_The_Total_Is_Reported()
    {
        var entry = Entry();
        var repository = RepositoryReturning([entry], 41);

        var handler = new GetAuditLogsQueryHandler(repository.Object);

        var result = await handler.Handle(
            new GetAuditLogsQuery(null, null, null, null, null, null, 2, 10),
            CancellationToken.None);

        var dto = Assert.Single(result.Data!);

        // The snapshot rides through exactly as stored -- the trail is not reshaped on the way out.
        Assert.Equal(entry.Id, dto.Id);
        Assert.Equal(entry.Action, dto.Action);
        Assert.Equal(entry.OldValues, dto.OldValues);
        Assert.Equal(entry.NewValues, dto.NewValues);
        Assert.Equal(entry.ActorUserId, dto.ActorUserId);

        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(41, result.TotalCount);
        Assert.Equal(5, result.TotalPages);
    }
}
