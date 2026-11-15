using OpsPilot.Contracts.AuditLogs;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class AuditLogMappings
{
    public static AuditLogDto ToDto(this AuditLog log) =>
        new(log.Id,
            log.Action,
            log.EntityType,
            log.EntityId,
            log.ActorUserId,
            log.OldValues,
            log.NewValues,
            log.IpAddress,
            log.CreatedAt);
}
