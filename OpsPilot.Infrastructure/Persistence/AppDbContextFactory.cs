using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> construct the context without booting the API host.
/// </summary>
/// <remarks>
/// Scaffolding migrations is a design-time-only concern, so it is kept out of the runtime
/// composition root. The connection string is a placeholder: <c>dotnet ef</c> needs a
/// provider to build the model but never connects to the database while generating a
/// migration.
/// </remarks>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=OpsPilot;Trusted_Connection=True;TrustServerCertificate=True;";

    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(DesignTimeConnectionString)
            .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();

        return new AppDbContext(optionsBuilder.Options, new TenantContext());
    }
}
