using MediatR;
using OpsPilot.Contracts.Tenants;

namespace OpsPilot.Application.Features.Tenants.Commands.ActivateTenant;

/// <summary>Brings a deactivated company back into use.</summary>
public sealed record ActivateTenantCommand(Guid TenantId) : IRequest<TenantDto>;
