namespace OpsPilot.Infrastructure;

/// <summary>Database lifecycle settings for a deployment (plan, "Migrations").</summary>
public sealed class DatabaseOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "Database";

    /// <summary>
    /// When true, a hosted initializer applies pending EF migrations at startup, with retries,
    /// so an empty SQL Server in compose becomes a usable schema without a separate step.
    /// Off by default so the test host and a developer already running migrations keep control.
    /// </summary>
    public bool MigrateOnStartup { get; set; }
}
