using OpsPilot.Infrastructure;

namespace OpsPilot.UnitTests.Infrastructure;

public class DatabaseOptionsTests
{
    [Fact]
    public void SectionName_Is_Database()
    {
        Assert.Equal("Database", DatabaseOptions.SectionName);
    }

    [Fact]
    public void DefaultOptions_MigrateOnStartup_IsFalse()
    {
        Assert.False(new DatabaseOptions().MigrateOnStartup);
    }
}
