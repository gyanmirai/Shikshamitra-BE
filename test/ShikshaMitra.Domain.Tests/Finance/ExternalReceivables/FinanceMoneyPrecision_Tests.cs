using ShikshaMitra.Finance.ExternalReceivables;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance;

public class FinanceMoneyPrecision_Tests
{
	[Fact]
	public void A_Valid_Large_Legacy_Whole_Amount_Should_Retain_Every_Digit()
	{
		Assert.Equal(9999999999999998m, FinanceMoneyPrecision.FromLegacyDouble(9999999999999998d));
	}

	[Fact]
	public void A_Representable_Large_Decimal_Should_Round_Trip_Through_The_Legacy_Wire_Type()
	{
		var legacy = FinanceMoneyPrecision.ToLegacyDouble(9999999999999998m);
		Assert.Equal(9999999999999998d, legacy);
		Assert.Equal(9999999999999998m, FinanceMoneyPrecision.FromLegacyDouble(legacy));
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(double.NegativeInfinity)]
	[InlineData(-1d)]
	[InlineData(0.001d)]
	[InlineData(0.30000000000000004d)]
	[InlineData(10000000000000000d)]
	[InlineData(1e30d)]
	public void Invalid_Legacy_Money_Should_Require_Review_Without_Rounding(double amount)
	{
		var exception = Assert.Throws<BusinessException>(() => FinanceMoneyPrecision.FromLegacyDouble(amount));
		Assert.Equal(FinanceMoneyErrorCodes.ReviewRequired, exception.Code);
	}

	[Fact]
	public void Ordinary_Recorded_Paise_Should_Stay_Exact()
	{
		Assert.Equal(0.30m, FinanceMoneyPrecision.FromLegacyDouble(0.30d));
		Assert.Equal(40.25m, FinanceMoneyPrecision.FromLegacyDouble(40.25d));
		Assert.Equal(40.25d, FinanceMoneyPrecision.ToLegacyDouble(40.25m));
	}

	[Fact]
	public void Decimal_Money_That_The_Legacy_Wire_Cannot_Represent_Should_Not_Be_Rounded()
	{
		var exception = Assert.Throws<BusinessException>(() => FinanceMoneyPrecision.ToLegacyDouble(9999999999999999.99m));
		Assert.Equal(FinanceMoneyErrorCodes.ReviewRequired, exception.Code);
	}
}
