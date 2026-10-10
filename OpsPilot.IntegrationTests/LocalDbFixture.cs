using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OpsPilot.Application.Abstractions;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.Infrastructure.Persistence.Interceptors;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Creates a throwaway SQL Server database on LocalDB, migrates it to the latest schema and
/// hands out contexts wired the same way the production composition root wires them.
/// </summary>
/// <remarks>
/// These tests exercise rules that only exist once a real database is involved: filtered
/// indexes, cascades, row-version concurrency and the save-changes interceptors. A model-only
/// test cannot see any of them, so the schema is applied for real rather than asserted about.
/// </remarks>
public sealed class LocalDbFixture : IAsyncLifetime
{
    private string _databaseName = string.Empty;
    private string _adminConnectionString = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _databaseName = $"OpsPilot_IT_{Guid.NewGuid():N}"[..24];
        _adminConnectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";

        await using (var admin = new SqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new SqlCommand($"CREATE DATABASE [{_databaseName}]", admin);
            await create.ExecuteNonQueryAsync();
        }

        ConnectionString =
            $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

        await using var context = CreateContext(new TenantContext());
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var admin = new SqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new SqlCommand(
            $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}];",
            admin);
        await drop.ExecuteNonQueryAsync();
    }

    /// <summary>Builds a context for <paramref name="tenant"/> with the production interceptors attached.</summary>
    public AppDbContext CreateContext(TenantContext tenant, ICurrentUser? user = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>()
            .AddInterceptors(
                new AuditingSaveChangesInterceptor(
                    user ?? new StubCurrentUser(null),
                    tenant,
                    TimeProvider.System),
                new AppendOnlySaveChangesInterceptor())
            .Options;

        return new AppDbContext(options, tenant);
    }

    /// <summary>
    /// Creates a company and returns a scope whose context reads only that company.
    /// </summary>
    /// <remarks>
    /// The tenant scope is established from the id the database actually assigned, not from an
    /// id the test made up. Entering a hand-picked id here would silently match nothing --
    /// the query filter fails closed -- and every assertion about visibility would then be
    /// asserting the failure case while appearing to pass.
    /// </remarks>
    public async Task<CompanyScope> CompanyAsync(string name = "Acme", ICurrentUser? user = null)
    {
        var tenants = new TenantContext();
        Seed.Company company;

        // Query filters do not apply to writes, so the company can be created before a tenant
        // is established. Reads afterwards need the real id, which only exists post-save.
        await using (var setup = CreateContext(tenants, user))
        {
            company = await Seed.CompanyAsync(setup, name);
        }

        var scope = tenants.Enter(company.Tenant.Id);
        return new CompanyScope(tenants, CreateContext(tenants, user), company, scope);
    }

    private sealed class StubCurrentUser(string? userId) : ICurrentUser
    {
        public string? UserId { get; } = userId;
    }
}

/// <summary>A company plus a context already scoped to it. Disposing tears down both.</summary>
public sealed class CompanyScope(
    TenantContext tenants,
    AppDbContext context,
    Seed.Company company,
    IDisposable scope) : IDisposable, IAsyncDisposable
{
    public AppDbContext Context { get; } = context;

    public Seed.Company Company { get; } = company;

    public Guid TenantId => Company.Tenant.Id;

    /// <summary>The ambient tenant, for tests that need extra contexts on the same company.</summary>
    public TenantContext Tenants { get; } = tenants;

    public void Dispose()
    {
        Context.Dispose();
        scope.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        scope.Dispose();
    }
}
