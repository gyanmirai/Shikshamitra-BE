using System;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class VariableFeePricing_Tests
{
    private static FeeStructureDetail Detail(decimal? amount, bool variable, bool optional = false) =>
        new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), amount, false, optional, null, null, variable);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Variable_Pricing_Has_No_Configured_Amount_And_Is_Independent_Of_Optional(bool optional)
    {
        var detail = Detail(null, true, optional);
        Assert.Null(detail.Amount);
        Assert.True(detail.IsVariable);
        Assert.Equal(optional, detail.IsOptional);
        Assert.Throws<BusinessException>(() => detail.UpdateAmount(100));
        detail.SetPricing(false, 100.01m);
        Assert.False(detail.IsVariable);
        Assert.Equal(100.01m, detail.Amount);
        detail.SetPricing(true, null);
        Assert.Null(detail.Amount);
    }

    [Fact]
    public void Contradictory_And_Invalid_Pricing_Is_Rejected()
    {
        Assert.Throws<BusinessException>(() => Detail(0, true));
        Assert.Throws<BusinessException>(() => Detail(null, false));
        Assert.Throws<BusinessException>(() => Detail(-1, false));
        Assert.Throws<BusinessException>(() => Detail(1.123m, false));
        Assert.Throws<BusinessException>(() => Detail(10000000000m, false));
        Assert.Equal(0m, Detail(0, false).Amount);
    }
}
