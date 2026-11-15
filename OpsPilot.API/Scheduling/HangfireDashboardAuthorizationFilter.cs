using Hangfire.Dashboard;
using Microsoft.Extensions.Options;
using OpsPilot.Infrastructure.BackgroundJobs;

namespace OpsPilot.API.Scheduling;

/// <summary>
/// Protects the Hangfire dashboard. Production (or any environment with a configured username)
/// requires HTTP Basic credentials; a local Development process with no username stays open so
/// the dashboard remains usable on a developer machine.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var options = http.RequestServices.GetRequiredService<IOptions<HangfireOptions>>().Value;
        var environment = http.RequestServices.GetRequiredService<IHostEnvironment>();

        var allowed = HangfireDashboardAccess.Allow(
            options,
            environment.IsProduction(),
            http.Request.Headers.Authorization);

        if (!allowed && !string.IsNullOrWhiteSpace(options.DashboardUsername))
        {
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            http.Response.Headers.WWWAuthenticate = "Basic realm=\"OpsPilot Hangfire\"";
        }

        return allowed;
    }
}
