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
/// <remarks>
/// <para>
/// The same pass is also called from the entry point before the host starts
/// (<see cref="MigrateIfConfiguredAsync"/>): the host builds every hosted service before it
/// starts any of them, and a service may touch the schema from its constructor. Hangfire prepares
/// its own schema the moment its storage is created, so on an empty database that construction
/// fails before this service would ever get a turn. Migrating first removes the race; running
/// again here is a fast no-op.
/// </para>
/// </remarks>
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

    public Task StartAsync(CancellationToken cancellationToken) =>
        MigrateAsync(_scopes, _options, _logger, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Runs the configured migration against <paramref name="services"/>. A no-op when
    /// <see cref="DatabaseOptions.MigrateOnStartup"/> is off.
    /// </summary>
    public static Task MigrateIfConfiguredAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (!options.MigrateOnStartup)
        {
            return Task.CompletedTask;
        }

        return MigrateAsync(
            services.GetRequiredService<IServiceScopeFactory>(),
            options,
            services.GetRequiredService<ILogger<DatabaseMigratorHostedService>>(),
            cancellationToken);
    }

    private static async Task MigrateAsync(
        IServiceScopeFactory scopes,
        DatabaseOptions options,
        ILogger<DatabaseMigratorHostedService> logger,
        CancellationToken cancellationToken)
    {
        if (!options.MigrateOnStartup)
        {
            return;
        }

        const int maxAttempts = 8;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await context.Database.MigrateAsync(cancellationToken);

                logger.LogInformation("Database schema is up to date.");
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, 2 * attempt));

                logger.LogWarning(
                    exception,
                    "Database migrate attempt {Attempt} of {Max} failed; retrying in {Delay}.",
                    attempt,
                    maxAttempts,
                    delay);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
