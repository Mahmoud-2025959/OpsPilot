using OpsPilot.Application.Features.SlaPolicies.Commands.UpdateSlaPolicies;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.UnitTests.Application;

public class UpdateSlaPoliciesCommandValidatorTests
{
    private readonly UpdateSlaPoliciesCommandValidator _validator = new();

    private static UpdateSlaPoliciesCommand Command(params SlaPolicyTarget[] policies) =>
        new(Guid.NewGuid(), policies);

    [Fact]
    public void A_Single_Valid_Policy_Passes()
    {
        var result = _validator.Validate(Command(new SlaPolicyTarget(Severity.Critical, 15, 240)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Several_Distinct_Severities_Pass()
    {
        var result = _validator.Validate(Command(
            new SlaPolicyTarget(Severity.Critical, 15, 240),
            new SlaPolicyTarget(Severity.Low, 480, 4320)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void An_Empty_Batch_Fails()
    {
        var result = _validator.Validate(Command());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateSlaPoliciesCommand.Policies));
    }

    [Fact]
    public void Duplicate_Severities_Fail()
    {
        var result = _validator.Validate(Command(
            new SlaPolicyTarget(Severity.Critical, 15, 240),
            new SlaPolicyTarget(Severity.Critical, 30, 480)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void An_Undefined_Severity_Fails()
    {
        var result = _validator.Validate(Command(new SlaPolicyTarget((Severity)999, 15, 240)));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0, 240)]
    [InlineData(-1, 240)]
    public void A_Non_Positive_Response_Fails(int response, int resolution)
    {
        var result = _validator.Validate(
            Command(new SlaPolicyTarget(Severity.Critical, response, resolution)));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(15, 0)]
    [InlineData(15, -5)]
    public void A_Non_Positive_Resolution_Fails(int response, int resolution)
    {
        var result = _validator.Validate(
            Command(new SlaPolicyTarget(Severity.Critical, response, resolution)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Resolution_Not_Greater_Than_Response_Fails()
    {
        var result = _validator.Validate(Command(new SlaPolicyTarget(Severity.High, 120, 120)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_Tenant_Id_Fails()
    {
        var result = _validator.Validate(
            new UpdateSlaPoliciesCommand(Guid.Empty, [new SlaPolicyTarget(Severity.Low, 480, 4320)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateSlaPoliciesCommand.TenantId));
    }
}
