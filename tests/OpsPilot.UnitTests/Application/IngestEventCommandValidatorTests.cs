using OpsPilot.Application.Features.Events.Commands.IngestEvent;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.UnitTests.Application;

public class IngestEventCommandValidatorTests
{
    private readonly IngestEventCommandValidator _validator = new();

    private static IngestEventCommand ValidCommand() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Payments",
            EnvironmentType.Production,
            Severity.High,
            "Database connection timeout");

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        Assert.True(_validator.Validate(ValidCommand()).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankService_FailsValidation(string? service)
    {
        var result = _validator.Validate(ValidCommand() with { Service = service! });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(IngestEventCommand.Service));
    }

    [Fact]
    public void BlankMessage_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { Message = "  " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(IngestEventCommand.Message));
    }

    [Fact]
    public void UndefinedEnvironment_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { Environment = (EnvironmentType)999 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(IngestEventCommand.Environment));
    }

    [Fact]
    public void UndefinedSeverity_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { Severity = (Severity)999 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(IngestEventCommand.Severity));
    }

    [Fact]
    public void MessageOverMaxLength_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { Message = new string('x', 2001) });

        Assert.False(result.IsValid);
    }
}
