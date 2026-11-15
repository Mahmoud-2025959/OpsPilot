using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpsPilot.Infrastructure.Persistence;

namespace OpsPilot.API.Health;

/// <summary>
/// Readiness probe: reports healthy only when the database can be reached, so a load balancer
/// withholds traffic from an instance that cannot serve requests.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public DatabaseHealthCheck(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy("Database is unreachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", exception);
        }
    }
}
