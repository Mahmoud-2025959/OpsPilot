using System.Reflection;
using NetArchTest.Rules;
using OpsPilot.Domain.Entities;
using Xunit;

namespace OpsPilot.ArchitectureTests;

public class ArchitectureTests
{
    private const string ApplicationNs = "OpsPilot.Application";
    private const string ContractsNs = "OpsPilot.Contracts";
    private const string DomainNs = "OpsPilot.Domain";
    private const string InfrastructureNs = "OpsPilot.Infrastructure";
    private const string ApiNs = "OpsPilot.API";

    private static readonly Assembly DomainAssembly = typeof(Incident).Assembly;
    private static readonly Assembly ApplicationAssembly = Assembly.Load(ApplicationNs);
    private static readonly Assembly ContractsAssembly = Assembly.Load(ContractsNs);
    private static readonly Assembly InfrastructureAssembly = Assembly.Load(InfrastructureNs);

    private static void Ensure(TestResult result)
    {
        var failures = result.FailingTypeNames ?? [];
        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Any_Other_Layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, ContractsNs, InfrastructureNs, ApiNs)
            .GetResult();

        Ensure(result);
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_Any_Other_Layer()
    {
        var result = Types.InAssembly(ContractsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, DomainNs, InfrastructureNs, ApiNs)
            .GetResult();

        Ensure(result);
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Api_Or_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApiNs, InfrastructureNs)
            .GetResult();

        Ensure(result);
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNs)
            .GetResult();

        Ensure(result);
    }

    [Fact]
    public void Api_Should_Not_Be_Referred_To_By_Inner_Layers()
    {
        var result = Types.InAssemblies(
                new[] { DomainAssembly, ApplicationAssembly, ContractsAssembly, InfrastructureAssembly })
            .ShouldNot()
            .HaveDependencyOn(ApiNs)
            .GetResult();

        Ensure(result);
    }
}