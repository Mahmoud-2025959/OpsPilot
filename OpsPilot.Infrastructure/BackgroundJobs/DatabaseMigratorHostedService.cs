using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsPilot.Infrastructure.Persistence;

namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Applies pending EF migrations at process start when <see cref="DatabaseOptions.MigrateOnStartup"/>
/// is on. A compose stack with an empty SQL volume depends on this rather than a human running
/// <c>dotnet ef database update</c> first.
/// </summary>
public sealed class DatabaseMigratorHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseMigratorHostedService> _logger;

    public DatabaseMigratorHostedService(
        IServiceScopeFactory scopes,
        IOptions<DatabaseOptions> options,
        ILogger<DatabaseMigratorHostedService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.MigrateOnStartup)
        {
            return;
        }

        const int maxAttempts = 8;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await context.Database.MigrateAsync(cancellationToken);

                _logger.LogInformation("Database schema is up to date.");
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, 2 * attempt));

                _logger.LogWarning(
                    exception,
                    "Database migrate attempt {Attempt} of {Max} failed; retrying in {Delay}.",
                    attempt,
                    maxAttempts,
                    delay);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
