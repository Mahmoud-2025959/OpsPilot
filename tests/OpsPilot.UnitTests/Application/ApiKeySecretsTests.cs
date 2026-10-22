using OpsPilot.Application.Features.ApiKeys;
using Xunit;

namespace OpsPilot.UnitTests.Application;

public class ApiKeySecretsTests
{
    [Fact]
    public void Generated_Key_Has_The_Expected_Shape()
    {
        var raw = ApiKeySecrets.GenerateRawKey();

        Assert.StartsWith("op_live_", raw);
        // "op_live_" plus 16 random bytes rendered as 32 hex characters.
        Assert.Equal(40, raw.Length);
    }

    [Fact]
    public void Generated_Keys_Are_Distinct()
    {
        Assert.NotEqual(ApiKeySecrets.GenerateRawKey(), ApiKeySecrets.GenerateRawKey());
    }

    [Fact]
    public void The_Prefix_Is_The_Leading_Segment_And_Fits_The_Column()
    {
        var raw = ApiKeySecrets.GenerateRawKey();
        var prefix = ApiKeySecrets.PrefixOf(raw);

        Assert.Equal(raw[..ApiKeySecrets.PrefixLength], prefix);
        Assert.True(prefix.Length <= 16);
        Assert.StartsWith("op_live_", prefix);
    }

    [Fact]
    public void Hashing_Is_Deterministic_And_Key_Specific()
    {
        var raw = ApiKeySecrets.GenerateRawKey();

        Assert.Equal(ApiKeySecrets.Hash(raw), ApiKeySecrets.Hash(raw));
        Assert.NotEqual(ApiKeySecrets.Hash(raw), ApiKeySecrets.Hash(ApiKeySecrets.GenerateRawKey()));
    }

    [Fact]
    public void The_Hash_Does_Not_Contain_The_Secret()
    {
        var raw = ApiKeySecrets.GenerateRawKey();
        var hash = ApiKeySecrets.Hash(raw);

        Assert.NotEqual(raw, hash);
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }
}
