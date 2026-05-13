using ManufacturingERP.Application.Services;
using Xunit;

namespace ManufacturingERP.Tests;

public class PasswordHasherServiceTests
{
    [Fact]
    public void Hash_And_Verify_Should_Work()
    {
        var service = new PasswordHasherService();
        var hash = service.Hash("admin123");

        Assert.NotEqual("admin123", hash);
        Assert.StartsWith("PBKDF2-SHA256$", hash, StringComparison.Ordinal);
        Assert.True(service.Verify("admin123", hash));
        Assert.False(service.Verify("wrong", hash));
        Assert.False(service.NeedsRehash(hash));
    }

    [Fact]
    public void Verify_Should_Support_LegacySha256_And_RequestRehash()
    {
        var service = new PasswordHasherService();
        var legacyHash = "240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9";

        Assert.True(service.Verify("admin123", legacyHash));
        Assert.True(service.NeedsRehash(legacyHash));
    }
}
