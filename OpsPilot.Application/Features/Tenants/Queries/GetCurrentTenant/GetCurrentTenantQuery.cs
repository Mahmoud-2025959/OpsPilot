using MediatR;
using OpsPilot.Contracts.Tenants;

namespace OpsPilot.Application.Features.Tenants.Queries.GetCurrentTenant;

/// <summary>Reads the company the request belongs to.</summary>
public sealed record GetCurrentTenantQuery(Guid TenantId) : IRequest<TenantDto>;
