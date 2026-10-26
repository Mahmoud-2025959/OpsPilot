using MediatR;
using OpsPilot.Contracts.Tenants;

namespace OpsPilot.Application.Features.Tenants.Commands.RenameTenant;

/// <summary>Renames the company.</summary>
public sealed record RenameTenantCommand(Guid TenantId, string Name) : IRequest<TenantDto>;
