using Microsoft.EntityFrameworkCore;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Creates the minimum graph a test needs: a tenant, a user in it, and a service owned by
/// that tenant. Each call uses a fresh tenant so tests cannot see each other's rows.
/// </summary>
public static class Seed
{
    public static async Task<Company> CompanyAsync(AppDbContext context, string name = "Acme")
    {
        var tenant = Tenant.Create(name, $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}"[..20]);
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        var user = User.Create(tenant.Id, $"user-{Guid.NewGuid():N}@acme.test"[..30], "Test User");
        context.Users.Add(user);

        var service = Service.Create(tenant.Id, "Payments", "payments", EnvironmentType.Production);
        context.Services.Add(service);

        await context.SaveChangesAsync();

        return new Company(tenant, user, service);
    }

    public sealed record Company(Tenant Tenant, User User, Service Service);

    /// <summary>Builds an incident for the company, optionally joining a correlation key.</summary>
    public static Incident AnIncident(
        this Company company,
        string title = "Checkout latency spike",
        string? correlationKey = null) => Incident.Create(
            company.Tenant.Id,
            company.Service.Id,
            title,
            "p99 latency above threshold",
            Severity.High,
            company.User.Id,
            DateTimeOffset.UtcNow,
            correlationKey);
}
