using System.Security.Claims;
using OpsPilot.Application.Abstractions;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.API.Middleware;

/// <summary>
/// Establishes the ambient tenant for the request from the <c>tenant_id</c> claim embedded in
/// the access token.
/// </summary>
/// <remarks>
/// <para>
/// This is the piece that makes tenant isolation real: the global query filter in
/// <c>AppDbContext</c> reads <see cref="ICurrentTenant.TenantId"/>, and until this middleware
/// runs that value is <see cref="Guid.Empty"/>, which matches nothing. Without it every
/// tenant-filtered read — including the login lookup for a tenant-owned <c>User</c> — silently
/// returns no rows.
/// </para>
/// <para>
/// It must run after <c>UseAuthentication</c> (the claim is only populated once the bearer
/// token has been validated) and before <c>UseAuthorization</c>. Anonymous requests have no
/// tenant and pass through unchanged; the small number of flows that legitimately cross
/// tenants (login by email, refresh by token hash) opt out explicitly in their repositories.
/// </para>
/// </remarks>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenant currentTenant)
    {
        var claimValue = context.User.FindFirst(AuthClaimTypes.TenantId)?.Value;

        if (Guid.TryParse(claimValue, out var tenantId) && tenantId != Guid.Empty)
        {
            // Scoped to the request: the tenant is restored when the response is written.
            using var scope = currentTenant.Enter(tenantId);
            await _next(context);
            return;
        }

        await _next(context);
    }
}
