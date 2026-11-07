using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Runs the real HTTP pipeline against the fixture's throwaway database.
/// </summary>
/// <remarks>
/// The connection string is read by <c>WebApplication.CreateBuilder</c>, so it is supplied both
/// as host configuration and as a late in-memory source: whichever composition root the host
/// picks up, it resolves to the throwaway database rather than the developer's own.
/// </remarks>
/// <param name="db">The database to point the host at.</param>
/// <param name="slaWorkerEnabled">
/// Turns the SLA sweep on so its wiring can be exercised. Off by default: a timer that writes
/// breaches and notifications into the shared fixture database would make the counts other
/// tests assert on wobble. Its rules are covered from their own tests instead.
/// </param>
/// <param name="ingestionPermitLimit">
/// How many events a key may post per window. 100 is the plan's figure; tests shrink it so a
/// 429 can be provoked without posting a hundred events first.
/// </param>
public sealed class IntegrationApiFactory(
    LocalDbFixture db,
    bool slaWorkerEnabled = false,
    int ingestionPermitLimit = 100)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = db.ConnectionString,

            // Feature switches are read at two different moments. `AddInfrastructure` reads
            // them while registering services, and the hosted services read them again at
            // runtime. `ConfigureAppConfiguration` below only reaches the second: WebApplicationFactory
            // installs it after Program has already called AddInfrastructure. Anything that
            // decides *whether a service is registered* therefore has to be a host setting too,
            // which `UseSetting` supplies before Program's body runs.
            ["SlaViolationWorker:Enabled"] = slaWorkerEnabled ? "true" : "false",

            // Even when the sweep is switched on, its timer is parked a day out: a test host
            // must never record rows behind the back of the test that is running.
            ["SlaViolationWorker:Interval"] = "1.00:00:00",

            // The outbox dispatcher is off for the same reason; a test drives a pass itself.
            ["OutboxProcessor:Enabled"] = "false",

            // The startup permission sweep is off: it would make a pass over every company
            // in the shared fixture database for every host a test builds. Its behaviour is
            // covered by PermissionCatalogSyncTests, which drives the pass directly.
            ["Permissions:Enabled"] = "false",

            // So does the email sweep: its timer would deliver rows into the shared
            // fixture database behind the back of the test that is asserting on them.
            ["EmailDispatch:Enabled"] = "false",

            // And the webhook sweep, for the same reason: it would call endpoints behind
            // the back of the test that is asserting on them.
            ["WebhookDispatch:Enabled"] = "false",

            // Hangfire is the scheduler only when it is switched on, and it must never be on
            // in a test host: it would both own the schedules above and create its own tables
            // in the fixture database. Off here regardless of what appsettings says.
            ["Hangfire:Enabled"] = "false",

            // Development now points SMTP at smtp4dev. A test host must not open that
            // socket: EmailDispatchTests and ScheduledJobsTests assert the switched-off
            // path, and a missing listener would burn attempts on shared fixture rows.
            ["Smtp:Enabled"] = "false",

            // Migrations are applied by LocalDbFixture. The hosted initializer must stay off.
            ["Database:MigrateOnStartup"] = "false",

            // A broker is never wired for a test: the logging publisher is what the outbox
            // tests exercise, and no test should need a container to run. ConsumerEnabled
            // must be off as well — otherwise the consumer still registers its singleton
            // connection and a hosted service, even with publishing disabled.
            ["RabbitMq:Enabled"] = "false",
            ["RabbitMq:ConsumerEnabled"] = "false",

            ["RateLimiting:Ingestion:PermitLimit"] =
                ingestionPermitLimit.ToString(CultureInfo.InvariantCulture),

            // The key that protects the stored copy of an ingestion key. A test host needs
            // one, and it needs it to be the same one every run: a key encrypted under a
            // different padlock would not read back, and the tests that read keys back
            // would fail for a reason that has nothing to do with the code under test.
            ["ApiKeyEncryption:Key"] = "KioqKioqKioqKioqKioqKioqKioqKioqKioqKioqKio=",
        };

        foreach (var setting in settings)
        {
            builder.UseSetting(setting.Key, setting.Value);
        }

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(settings));
    }
}
