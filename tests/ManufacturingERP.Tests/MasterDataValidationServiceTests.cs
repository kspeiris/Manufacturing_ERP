using ManufacturingERP.Application.Services;
using Xunit;

namespace ManufacturingERP.Tests;

public class MasterDataValidationServiceTests
{
    [Fact]
    public void ValidateRequired_Should_Fail_For_Blank()
    {
        var service = new MasterDataValidationService();
        var result = service.ValidateRequired("Name", "");
        Assert.False(result.ok);
    }

    [Fact]
    public void ValidateNonNegative_Should_Fail_For_Negative()
    {
        var service = new MasterDataValidationService();
        var result = service.ValidateNonNegative("Amount", -1);
        Assert.False(result.ok);
    }
}
