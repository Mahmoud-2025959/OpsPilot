using MediatR;
using OpsPilot.Contracts.AuditLogs;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.AuditLogs.Queries.GetAuditLogs;

/// <summary>
/// Reads the tenant's audit trail, newest first. The tenant is never supplied by the caller;
/// the global query filter guarantees it (plan, UC-14 and FR-18).
/// </summary>
public sealed record GetAuditLogsQuery(
    string? Action,
    string? EntityType,
    string? EntityId,
    Guid? ActorUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResponse<AuditLogDto>>;
