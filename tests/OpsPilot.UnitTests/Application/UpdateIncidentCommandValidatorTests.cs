using OpsPilot.Application.Features.Incidents.Commands.UpdateIncident;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.UnitTests.Application;

public class UpdateIncidentCommandValidatorTests
{
    private readonly UpdateIncidentCommandValidator _validator = new();

    private static UpdateIncidentCommand ValidCommand() =>
        new(Guid.NewGuid(), "Checkout latency", "p99 above threshold", Severity.High);

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        var result = _validator.Validate(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyIncidentId_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { IncidentId = Guid.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateIncidentCommand.IncidentId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankTitle_FailsValidation(string? title)
    {
        var result = _validator.Validate(ValidCommand() with { Title = title! });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateIncidentCommand.Title));
    }

    [Fact]
    public void UndefinedSeverity_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { Severity = (Severity)999 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateIncidentCommand.Severity));
    }
}
