using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

public class PersistenceModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=OpsPilot_ModelOnly;Trusted_Connection=True;")
            .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>()
            .Options;

        return new AppDbContext(options, new TenantContext());
    }

    [Fact]
    public void Model_Can_Be_Built_With_All_Entities()
    {
        using var context = CreateContext();

        var entityNames = context.Model.GetEntityTypes()
            .Select(e => e.ClrType.Name)
            .ToHashSet();

        Assert.Contains("User", entityNames);
        Assert.Contains("Role", entityNames);
        Assert.Contains("Permission", entityNames);
        Assert.Contains("RefreshToken", entityNames);
        Assert.Contains("Tenant", entityNames);
        Assert.Contains("Team", entityNames);
        Assert.Contains("TeamMember", entityNames);
    }

    [Fact]
    public void User_Role_Is_Many_To_Many()
    {
        using var context = CreateContext();

        var user = context.Model.FindEntityType("OpsPilot.Domain.Entities.User");
        Assert.NotNull(user);

        var roleNavigation = user!.GetSkipNavigations()
            .Single(n => n.Name == "Roles");

        Assert.True(roleNavigation.IsCollection);
        Assert.Equal("Role", roleNavigation.TargetEntityType.ClrType.Name);
    }

    [Fact]
    public void Tenant_Has_One_To_Many_Relationships()
    {
        using var context = CreateContext();

        var user = context.Model.FindEntityType("OpsPilot.Domain.Entities.User");
        var tenantForeignKey = user!.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType.Name == "Tenant");

        Assert.NotNull(tenantForeignKey);
    }

    [Fact]
    public void RefreshToken_References_User()
    {
        using var context = CreateContext();

        var refreshToken = context.Model.FindEntityType("OpsPilot.Domain.Entities.RefreshToken");
        var userForeignKey = refreshToken!.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType.Name == "User");

        Assert.NotNull(userForeignKey);
    }
}
