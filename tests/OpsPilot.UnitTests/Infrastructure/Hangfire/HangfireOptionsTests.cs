using OpsPilot.Infrastructure.BackgroundJobs;

namespace OpsPilot.UnitTests.Infrastructure.Hangfire;

/// <summary>
/// Tests for <see cref="HangfireOptions"/> defaults, section name, and cron expressions.
/// </summary>
public class HangfireOptionsTests
{
    // ── section name ──────────────────────────────────────────────────────────

    [Fact]
    public void SectionName_Is_Hangfire()
    {
        // ScheduledJobsExtensions and DependencyInjection bind from this constant.
        Assert.Equal("Hangfire", HangfireOptions.SectionName);
    }

    // ── defaults ──────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultOptions_Enabled_IsFalse()
    {
        // A fresh clone must start without requiring a Hangfire schema.
        var options = new HangfireOptions();
        Assert.False(options.Enabled);
    }

    [Fact]
    public void DefaultOptions_DashboardEnabled_IsTrue()
    {
        // The dashboard is the primary reason to adopt Hangfire, so it is on by default.
        var options = new HangfireOptions();
        Assert.True(options.DashboardEnabled);
    }

    [Fact]
    public void DefaultOptions_DashboardPath_IsJobs()
    {
        var options = new HangfireOptions();
        Assert.Equal("/jobs", options.DashboardPath);
    }

    [Fact]
    public void DefaultOptions_SlaSweepCron_IsEveryMinute()
    {
        // Must match the SlaViolationDetectionWorker's own timer interval.
        var options = new HangfireOptions();
        Assert.Equal("* * * * *", options.SlaSweepCron);
    }

    [Fact]
    public void DefaultOptions_OutboxDrainCron_IsEveryThirtySeconds()
    {
        // Must match OutboxProcessorOptions.Interval default (30 s).
        var options = new HangfireOptions();
        Assert.Equal("*/30 * * * * *", options.OutboxDrainCron);
    }

    [Fact]
    public void DefaultOptions_EmailDispatchCron_IsEveryThirtySeconds()
    {
        // Must match EmailDispatchOptions.Interval default (30 s).
        var options = new HangfireOptions();
        Assert.Equal("*/30 * * * * *", options.EmailDispatchCron);
    }

    [Fact]
    public void DefaultOptions_WebhookDispatchCron_IsEveryFifteenSeconds()
    {
        // Must match WebhookDispatchOptions.Interval default (15 s).
        var options = new HangfireOptions();
        Assert.Equal("*/15 * * * * *", options.WebhookDispatchCron);
    }

    [Fact]
    public void DefaultOptions_MaxAutomaticRetries_IsThree()
    {
        var options = new HangfireOptions();
        Assert.Equal(3, options.MaxAutomaticRetries);
    }

    [Fact]
    public void DefaultOptions_DashboardUsername_IsEmpty()
    {
        var options = new HangfireOptions();
        Assert.Equal(string.Empty, options.DashboardUsername);
        Assert.Equal(string.Empty, options.DashboardPassword);
    }

    // ── mutability (binding path) ─────────────────────────────────────────────

    [Fact]
    public void Options_CanBeEnabled()
    {
        var options = new HangfireOptions { Enabled = true };
        Assert.True(options.Enabled);
    }

    [Fact]
    public void Options_DashboardCanBeDisabled()
    {
        var options = new HangfireOptions { DashboardEnabled = false };
        Assert.False(options.DashboardEnabled);
    }

    [Fact]
    public void Options_CustomCronsArePreserved()
    {
        var options = new HangfireOptions
        {
            SlaSweepCron = "0 * * * *",
            OutboxDrainCron = "*/5 * * * * *",
            EmailDispatchCron = "0 8 * * *",
            WebhookDispatchCron = "*/10 * * * * *"
        };

        Assert.Equal("0 * * * *", options.SlaSweepCron);
        Assert.Equal("*/5 * * * * *", options.OutboxDrainCron);
        Assert.Equal("0 8 * * *", options.EmailDispatchCron);
        Assert.Equal("*/10 * * * * *", options.WebhookDispatchCron);
    }

    [Fact]
    public void Options_MaxAutomaticRetriesCanBeOverridden()
    {
        var options = new HangfireOptions { MaxAutomaticRetries = 10 };
        Assert.Equal(10, options.MaxAutomaticRetries);
    }

    // ── dashboard path format ─────────────────────────────────────────────────

    [Fact]
    public void DefaultDashboardPath_StartsWithSlash()
    {
        // ASP.NET MapHangfireDashboard requires a path starting with '/'.
        var options = new HangfireOptions();
        Assert.StartsWith("/", options.DashboardPath);
    }

    [Fact]
    public void DefaultDashboardPath_DoesNotSitUnderApi()
    {
        // The dashboard must not appear in the versioned OpenAPI document.
        var options = new HangfireOptions();
        Assert.DoesNotContain("/api", options.DashboardPath);
    }

    // ── recurring job ids (stable strings keyed in the Hangfire schema) ───────

    [Theory]
    [InlineData("sla-violation-sweep")]
    [InlineData("outbox-drain")]
    [InlineData("email-dispatch")]
    [InlineData("webhook-dispatch")]
    public void KnownJobIds_MatchKebabCaseConvention(string jobId)
    {
        // AddOrUpdate upserts by id; changing a value orphans existing rows in the DB.
        Assert.Matches(@"^[a-z][a-z0-9\-]+$", jobId);
    }
}
