using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Tenants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Tenants.Commands.DeactivateTenant;

public sealed class DeactivateTenantCommandHandler
    : IRequestHandler<DeactivateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;
    private readonly IAuditLogWriter _audit;

    public DeactivateTenantCommandHandler(
        ITenantRepository tenants,
        IUserRepository users,
        IAuditLogWriter audit)
    {
        _tenants = tenants;
        _users = users;
        _audit = audit;
    }

    public async Task<TenantDto> Handle(
        DeactivateTenantCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Tenant), request.TenantId);

        if (!tenant.IsActive)
        {
            return tenant.ToDto();
        }

        // The owner guard is taken before the company is closed, so the check and the change
        // are serialized against every other change that can alter the Owner set.
        await _users.ExecuteWithOwnerGuardAsync(
            async ct =>
            {
                // A company with no usable Owner could never be brought back, so one has to
                // survive the deactivation. The excluded id is empty because nobody is being
                // removed: the whole company is.
                if (!await _users.AnotherActiveOwnerExistsAsync(Guid.Empty, ct))
                {
                    throw new ConflictException(ExceptionMessages.CompanyWithoutOwner);
                }

                tenant.Deactivate();

                await _tenants.UpdateAsync(tenant, ct);
            },
            cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.TenantStatusChanged,
                nameof(Tenant),
                tenant.Id.ToString(),
                OldValues: new { isActive = true },
                NewValues: new { isActive = false }),
            cancellationToken);

        return tenant.ToDto();
    }
}
