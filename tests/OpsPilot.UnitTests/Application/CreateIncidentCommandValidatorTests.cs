using OpsPilot.Application.Features.Incidents.Commands;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.UnitTests.Application;

public class CreateIncidentCommandValidatorTests
{
    private readonly CreateIncidentCommandValidator _validator = new();

    private static CreateIncidentCommand ValidCommand() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "API is down",
            "5xx on /v1/events",
            Severity.High,
            Guid.NewGuid());

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        var result = _validator.Validate(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankTitle_FailsValidation(string? title)
    {
        var command = ValidCommand() with { Title = title! };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateIncidentCommand.Title));
    }

    [Fact]
    public void EmptyTenantId_FailsValidation()
    {
        var command = ValidCommand() with { TenantId = Guid.Empty };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateIncidentCommand.TenantId));
    }

    [Fact]
    public void UndefinedSeverity_FailsValidation()
    {
        var command = ValidCommand() with { Severity = (Severity)999 };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateIncidentCommand.Severity));
    }

    [Fact]
    public void TitleOverMaxLength_FailsValidation()
    {
        var command = ValidCommand() with { Title = new string('x', 201) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }
}