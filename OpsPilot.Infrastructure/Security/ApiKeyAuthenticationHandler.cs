using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.ApiKeys;
using OpsPilot.Contracts.Auth;
using OpsPilot.Domain.Identity;

namespace OpsPilot.Infrastructure.Security;

/// <summary>
/// Authenticates a non-human caller from the <c>X-API-Key</c> header (plan, §2.3: applications
/// have their own security path, a key instead of a JWT).
/// </summary>
/// <remarks>
/// <para>
/// The presented secret is hashed and looked up across tenants, because the tenant is only known
/// <em>after</em> the key resolves. The principal that comes back carries the same shape the JWT
/// path produces — a <c>tenant_id</c> claim that <c>TenantResolutionMiddleware</c> reads to enter
/// the tenant, and a permission claim — so everything downstream is unaware which scheme ran.
/// </para>
/// <para>
/// A key belongs to the user who issued it, and that user stands in as the reporter of any
/// incident the key opens (an incident's <c>CreatedBy</c> is a user, and a machine is not one).
/// A key that cannot be traced to an account is refused rather than allowed to create incidents
/// with no owner.
/// </para>
/// </remarks>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider)
        : base(options, logger, encoder)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var values))
        {
            // No key offered: let the pipeline fall through so the bearer scheme (or a challenge)
            // can decide what happens next.
            return AuthenticateResult.NoResult();
        }

        var presented = values.ToString();

        if (string.IsNullOrWhiteSpace(presented))
        {
            return AuthenticateResult.Fail("The X-API-Key header is empty.");
        }

        // A child scope keeps the request's own DbContext pristine: the tenant is not resolved
        // yet, and a context used before then would bake the empty tenant into its model for the
        // rest of the request.
        using var scope = _scopeFactory.CreateScope();
        var apiKeys = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

        var apiKey = await apiKeys.FindByKeyHashAcrossTenantsAsync(
            ApiKeySecrets.Hash(presented),
            Context.RequestAborted);

        if (apiKey is null || !apiKey.IsUsable())
        {
            return AuthenticateResult.Fail(ExceptionMessages.ApiKeyUnusable);
        }

        if (!Guid.TryParse(apiKey.CreatedBy, out var owner) || owner == Guid.Empty)
        {
            return AuthenticateResult.Fail(ExceptionMessages.ApiKeyNotAssociated);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, owner.ToString()),
            new(AuthClaimTypes.TenantId, apiKey.TenantId.ToString()),
            new(AuthClaimTypes.Permission, PermissionNames.EventsIngest),
            new(AuthClaimTypes.ApiKeyId, apiKey.Id.ToString())
        };

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.Scheme);

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            ApiKeyAuthenticationDefaults.Scheme);

        await RecordUsageAsync(apiKeys, apiKey);

        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// Stamps the key's last-used time. Best effort on purpose: a transient write failure must
    /// not reject a request that is otherwise valid.
    /// </summary>
    private async Task RecordUsageAsync(IApiKeyRepository apiKeys, Domain.Entities.ApiKey apiKey)
    {
        try
        {
            apiKey.MarkUsed(_timeProvider.GetUtcNow());
            await apiKeys.UpdateAsync(apiKey, Context.RequestAborted);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not record the last-used time for API key {ApiKeyId}.", apiKey.Id);
        }
    }
}
