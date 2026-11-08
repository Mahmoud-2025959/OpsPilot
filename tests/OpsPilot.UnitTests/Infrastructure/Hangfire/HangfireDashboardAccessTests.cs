using System.Text;
using OpsPilot.Infrastructure.BackgroundJobs;

namespace OpsPilot.UnitTests.Infrastructure.Hangfire;

public class HangfireDashboardAccessTests
{
    [Fact]
    public void Allow_WhenNoCredentials_AndNotProduction_IsTrue()
    {
        var options = new HangfireOptions();

        Assert.True(HangfireDashboardAccess.Allow(options, isProduction: false, authorizationHeader: null));
    }

    [Fact]
    public void Allow_WhenNoCredentials_AndProduction_IsFalse()
    {
        var options = new HangfireOptions();

        Assert.False(HangfireDashboardAccess.Allow(options, isProduction: true, authorizationHeader: null));
    }

    [Fact]
    public void Allow_WhenCredentialsConfigured_RequiresMatchingBasicHeader()
    {
        var options = new HangfireOptions
        {
            DashboardUsername = "ops",
            DashboardPassword = "s3cret"
        };

        Assert.False(HangfireDashboardAccess.Allow(options, isProduction: false, authorizationHeader: null));
        Assert.True(HangfireDashboardAccess.Allow(
            options,
            isProduction: true,
            authorizationHeader: Basic("ops", "s3cret")));
        Assert.False(HangfireDashboardAccess.Allow(
            options,
            isProduction: true,
            authorizationHeader: Basic("ops", "wrong")));
    }

    [Fact]
    public void MatchesBasic_RejectsMalformedHeader()
    {
        Assert.False(HangfireDashboardAccess.MatchesBasic("Bearer abc", "ops", "s3cret"));
        Assert.False(HangfireDashboardAccess.MatchesBasic("Basic not-base64", "ops", "s3cret"));
        Assert.False(HangfireDashboardAccess.MatchesBasic(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("nocolon")),
            "ops",
            "s3cret"));
    }

    private static string Basic(string username, string password) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
}
