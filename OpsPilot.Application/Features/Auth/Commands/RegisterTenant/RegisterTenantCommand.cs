using MediatR;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.Application.Features.Auth.Commands.RegisterTenant;

/// <summary>
/// Self-serve sign-up: creates the company, seeds its roles/permissions/SLA policies, and
/// makes the signer the Owner (ERD §3; plan's <c>POST /api/v1/auth/register-tenant</c>).
/// </summary>
public sealed record RegisterTenantCommand(
    string CompanyName,
    string? Slug,
    string Email,
    string DisplayName,
    string Password,
    string ConfirmPassword) : IRequest<AuthResponseDto>;
