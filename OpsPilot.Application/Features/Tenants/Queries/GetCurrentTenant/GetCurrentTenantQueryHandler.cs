using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Tenants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Tenants.Queries.GetCurrentTenant;

public sealed class GetCurrentTenantQueryHandler : IRequestHandler<GetCurrentTenantQuery, TenantDto>
{
    private readonly ITenantRepository _tenants;

    public GetCurrentTenantQueryHandler(ITenantRepository tenants) => _tenants = tenants;

    public async Task<TenantDto> Handle(
        GetCurrentTenantQuery request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Tenant), request.TenantId);

        return tenant.ToDto();
    }
}
