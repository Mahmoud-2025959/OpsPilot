using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Tenants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Tenants.Commands.ActivateTenant;

public sealed class ActivateTenantCommandHandler : IRequestHandler<ActivateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _tenants;
    private readonly IAuditLogWriter _audit;

    public ActivateTenantCommandHandler(ITenantRepository tenants, IAuditLogWriter audit)
    {
        _tenants = tenants;
        _audit = audit;
    }

    public async Task<TenantDto> Handle(
        ActivateTenantCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Tenant), request.TenantId);

        if (tenant.IsActive)
        {
            // Nothing changed, so nothing is written to the trail: the audit log records
            // what happened, not how many times a request was made.
            return tenant.ToDto();
        }

        tenant.Activate();

        await _tenants.UpdateAsync(tenant, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.TenantStatusChanged,
                nameof(Tenant),
                tenant.Id.ToString(),
                OldValues: new { isActive = false },
                NewValues: new { isActive = true }),
            cancellationToken);

        return tenant.ToDto();
    }
}
