using System.Globalization;
using ShikshaMitra.Library.Catalogue;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCataloguePrice_Tests
{
	[Fact]
	public void An_Unchanged_Historic_Negative_Price_Should_Survive_An_Unrelated_Metadata_Edit()
	{
		Assert.Equal(-12.50m, LibraryCataloguePriceRules.ResolveUpdate(-12.50m, null, "-12.50"));
		Assert.Equal(-12.50m, LibraryCataloguePriceRules.ResolveUpdate(-12.50m, -12.50m, null));
		Assert.Equal(-12.50m, LibraryCataloguePriceRules.ResolveUpdate(-12.50m, -12.50m, "-12.50"));
	}

	[Fact]
	public void Clearing_Or_Correcting_A_Historic_Price_Should_Require_An_Explicit_New_Value()
	{
		Assert.Null(LibraryCataloguePriceRules.ResolveUpdate(-12.50m, null, null));
		Assert.Equal(0m, LibraryCataloguePriceRules.ResolveUpdate(-12.50m, null, "0.00"));
		Assert.Equal(12.50m, LibraryCataloguePriceRules.ResolveUpdate(-12.50m, null, "12.50"));
	}

	[Theory]
	[InlineData("-12.51", null)]
	[InlineData("-12.50", "12.50")]
	[InlineData(" -12.50 ", null)]
	public void A_Historic_Negative_Price_Should_Not_Allow_A_Changed_Conflicting_Or_Coerced_Negative(string exact, string? numeric)
	{
		Assert.Throws<BusinessException>(() => LibraryCataloguePriceRules.ResolveUpdate(-12.50m,
			numeric == null ? null : decimal.Parse(numeric, CultureInfo.InvariantCulture), exact));
	}

	[Fact]
	public void Reading_An_Existing_Negative_Price_Should_Preserve_The_Stored_Value_Without_Allowing_A_New_Negative_Input()
	{
		Assert.Equal("-12.50", LibraryCataloguePriceRules.Format(-12.50m));
		Assert.Throws<BusinessException>(() => LibraryCataloguePriceRules.Resolve(-12.50m, null));
	}

	[Fact]
	public void An_Unknown_Catalogue_Price_Should_Stay_Unknown_Without_Becoming_Zero()
	{
		Assert.Null(LibraryCataloguePriceRules.Resolve(null, null));
		Assert.Null(LibraryCataloguePriceRules.Format(null));
	}

	[Fact]
	public void An_Existing_Or_Explicit_Zero_Price_Should_Remain_A_Known_Zero()
	{
		Assert.Equal(0m, LibraryCataloguePriceRules.Resolve(0m, null));
		Assert.Equal(0m, LibraryCataloguePriceRules.Resolve(null, "0.00"));
		Assert.Equal("0.00", LibraryCataloguePriceRules.Format(0m));
	}

	[Theory]
	[InlineData("12.50")]
	[InlineData("9999999999999999.99")]
	public void A_Known_Price_Should_Retain_Exact_Paise_Through_Both_Contracts(string raw)
	{
		var expected = decimal.Parse(raw, CultureInfo.InvariantCulture);
		Assert.Equal(expected, LibraryCataloguePriceRules.Resolve(null, raw));
		Assert.Equal(expected, LibraryCataloguePriceRules.Resolve(expected, raw));
		Assert.Equal(expected, LibraryCataloguePriceRules.Resolve(expected, null));
		Assert.Equal(raw, LibraryCataloguePriceRules.Format(expected));
	}

	[Fact]
	public void Conflicting_Numeric_And_Exact_Price_Should_Be_Rejected_Instead_Of_Choosing_One()
	{
		var error = Assert.Throws<BusinessException>(() => LibraryCataloguePriceRules.Resolve(12.50m, "12.51"));
		Assert.Equal(LibraryErrorCodes.CatalogueMetadataInvalid, error.Code);
		Assert.Equal("price", error.Data["Field"]);
	}

	[Theory]
	[InlineData("")]
	[InlineData("-0.01")]
	[InlineData("0.001")]
	[InlineData("10000000000000000.00")]
	[InlineData("1,200.00")]
	[InlineData(" 12.50 ")]
	[InlineData("1e2")]
	[InlineData("₹12.50")]
	public void Malformed_Or_Unrepresentable_Exact_Prices_Should_Be_Rejected_Without_Rounding(string value)
	{
		Assert.Throws<BusinessException>(() => LibraryCataloguePriceRules.Resolve(null, value));
	}

	[Theory]
	[InlineData("-0.01")]
	[InlineData("0.001")]
	[InlineData("10000000000000000.00")]
	public void Legacy_Numeric_Inputs_Should_Use_The_Same_Precision_And_Range_Boundary(string value)
	{
		Assert.Throws<BusinessException>(() => LibraryCataloguePriceRules.Resolve(decimal.Parse(value, CultureInfo.InvariantCulture), null));
	}
}
