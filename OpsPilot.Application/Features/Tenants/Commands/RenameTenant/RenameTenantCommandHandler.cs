using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Tenants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Tenants.Commands.RenameTenant;

public sealed class RenameTenantCommandHandler : IRequestHandler<RenameTenantCommand, TenantDto>
{
    private readonly ITenantRepository _tenants;
    private readonly IAuditLogWriter _audit;

    public RenameTenantCommandHandler(ITenantRepository tenants, IAuditLogWriter audit)
    {
        _tenants = tenants;
        _audit = audit;
    }

    public async Task<TenantDto> Handle(
        RenameTenantCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Tenant), request.TenantId);

        var from = tenant.Name;

        tenant.Rename(request.Name);

        await _tenants.UpdateAsync(tenant, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.TenantRenamed,
                nameof(Tenant),
                tenant.Id.ToString(),
                OldValues: new { name = from },
                NewValues: new { name = tenant.Name }),
            cancellationToken);

        return tenant.ToDto();
    }
}
