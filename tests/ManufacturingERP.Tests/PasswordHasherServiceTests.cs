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
        Assert.True(service.Verify("admin123", hash));
        Assert.False(service.Verify("wrong", hash));
    }
}
