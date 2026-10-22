using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.UnitTests.Domain;

/// <summary>
/// The correlation key is what stops one outage from becoming a page full of incidents, so the
/// matching rule has to be right in both directions: messages that differ only in their variable
/// parts must agree, and genuinely different faults must not.
/// </summary>
public class CorrelationKeyTests
{
    private static readonly Guid ServiceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Messages_Differing_Only_By_Numbers_Share_A_Key()
    {
        var earlier = CorrelationKey.Compute(ServiceId, EnvironmentType.Production, "Timed out after 30s");
        var later = CorrelationKey.Compute(ServiceId, EnvironmentType.Production, "Timed out after 31s");

        Assert.Equal(earlier, later);
    }

    [Theory]
    [InlineData("Timed out after 30s", "timed out after 31s")]
    [InlineData("  DISK   full  ", "disk full")]
    [InlineData("Trace id 0000 failed", "trace id 9999 failed")]
    public void Normalization_Makes_Variable_Parts_Irrelevant(string left, string right)
    {
        Assert.Equal(CorrelationKey.Normalize(left), CorrelationKey.Normalize(right));
    }

    [Fact]
    public void Different_Services_Do_Not_Share_A_Key()
    {
        var key = CorrelationKey.Compute(ServiceId, EnvironmentType.Production, "timeout");
        var other = CorrelationKey.Compute(Guid.NewGuid(), EnvironmentType.Production, "timeout");

        Assert.NotEqual(key, other);
    }

    [Fact]
    public void Different_Environments_Do_Not_Share_A_Key()
    {
        var production = CorrelationKey.Compute(ServiceId, EnvironmentType.Production, "timeout");
        var staging = CorrelationKey.Compute(ServiceId, EnvironmentType.Staging, "timeout");

        Assert.NotEqual(production, staging);
    }

    [Fact]
    public void Different_Messages_Do_Not_Share_A_Key()
    {
        var timeout = CorrelationKey.Compute(ServiceId, EnvironmentType.Production, "connection timeout");
        var refused = CorrelationKey.Compute(ServiceId, EnvironmentType.Production, "connection refused");

        Assert.NotEqual(timeout, refused);
    }

    [Fact]
    public void The_Digest_Fits_The_Column_And_Is_Stable()
    {
        var key = CorrelationKey.Compute(ServiceId, EnvironmentType.Production, "timeout");

        // The incident column is 128 characters; a 64-character lowercase hex digest fits with
        // room to spare and is deterministic.
        Assert.Equal(64, key.Length);
        Assert.Matches("^[0-9a-f]{64}$", key);
        Assert.Equal(key, CorrelationKey.Compute(ServiceId, EnvironmentType.Production, "timeout"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_Blank_Message_Is_Rejected(string? message)
    {
        // null surfaces as ArgumentNullException and a whitespace string as ArgumentException;
        // both derive from ArgumentException, so the looser assertion covers either.
        Assert.ThrowsAny<ArgumentException>(
            () => CorrelationKey.Compute(ServiceId, EnvironmentType.Production, message!));
    }
}
