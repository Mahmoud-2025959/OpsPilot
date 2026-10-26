using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Auth.Commands.RegisterTenant;
using Xunit;

namespace OpsPilot.UnitTests.Application;

public class RegisterTenantCommandValidatorTests
{
    private readonly RegisterTenantCommandValidator _validator = new();

    private static RegisterTenantCommand ValidCommand() =>
        new(
            "Globex Corporation",
            "globex",
            "owner@globex.test",
            "Hank Scorpio",
            "Sup3rSecret!",
            "Sup3rSecret!");

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        var result = _validator.Validate(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SlugIsOptional()
    {
        var result = _validator.Validate(ValidCommand() with { Slug = null });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankCompanyName_FailsValidation(string? companyName)
    {
        var result = _validator.Validate(ValidCommand() with { CompanyName = companyName! });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterTenantCommand.CompanyName));
    }

    [Theory]
    [InlineData("Has Spaces")]
    [InlineData("Has_Uppercase")]
    [InlineData("-leading-dash")]
    public void UnsafeSlug_FailsValidation(string slug)
    {
        var result = _validator.Validate(ValidCommand() with { Slug = slug });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterTenantCommand.Slug));
    }

    [Fact]
    public void MismatchedConfirmation_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { ConfirmPassword = "something-else" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterTenantCommand.ConfirmPassword));
    }

    [Fact]
    public void ShortPassword_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { Password = "short", ConfirmPassword = "short" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterTenantCommand.Password));
    }
}
