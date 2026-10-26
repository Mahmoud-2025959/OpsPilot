using MediatR;
using OpsPilot.Contracts.Tenants;

namespace OpsPilot.Application.Features.Tenants.Commands.DeactivateTenant;

/// <summary>
/// Closes the company: its people can no longer sign in, and its data stays exactly where it
/// is until somebody brings it back.
/// </summary>
public sealed record DeactivateTenantCommand(Guid TenantId) : IRequest<TenantDto>;
