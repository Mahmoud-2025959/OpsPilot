using OpsPilot.Application.Features.ApiKeys.Commands.CreateApiKey;
using Xunit;

namespace OpsPilot.UnitTests.Application;

public class CreateApiKeyCommandValidatorTests
{
    private readonly CreateApiKeyCommandValidator _validator = new();

    [Fact]
    public void Command_Without_An_Expiry_Passes()
    {
        var result = _validator.Validate(new CreateApiKeyCommand(Guid.NewGuid(), "monitoring"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Command_With_A_Future_Expiry_Passes()
    {
        var result = _validator.Validate(
            new CreateApiKeyCommand(Guid.NewGuid(), "monitoring", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void An_Expiry_In_The_Past_Fails()
    {
        var result = _validator.Validate(
            new CreateApiKeyCommand(Guid.NewGuid(), "monitoring", DateTimeOffset.UtcNow.AddDays(-1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateApiKeyCommand.ExpiresAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankName_Fails(string? name)
    {
        var result = _validator.Validate(new CreateApiKeyCommand(Guid.NewGuid(), name!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateApiKeyCommand.Name));
    }

    [Fact]
    public void NameOverMaxLength_Fails()
    {
        var result = _validator.Validate(new CreateApiKeyCommand(Guid.NewGuid(), new string('x', 201)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void EmptyTenantId_Fails()
    {
        var result = _validator.Validate(new CreateApiKeyCommand(Guid.Empty, "monitoring"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateApiKeyCommand.TenantId));
    }
}
