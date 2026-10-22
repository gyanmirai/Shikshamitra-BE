using ShikshaMitra.Finance;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class FinanceName_Tests
{
	[Theory]
	[InlineData("  8th  FEE\tCATEGORY  ", "8th FEE CATEGORY")]
	[InlineData("Policy\u00a0\u00a0Name", "Policy Name")]
	public void Normalizes_Whitespace_And_Case_For_Comparison(string input, string expected)
	{
		FinanceName.Normalize(input).ShouldBe(expected);
		FinanceName.Equals(input, expected.ToLowerInvariant()).ShouldBeTrue();
	}

	[Fact]
	public void Copy_Uses_Next_Available_Name_Including_Case_And_Whitespace_Variants()
	{
		FinanceName.NextCopy("Sibling support", ["Sibling support Copy", " SIBLING  SUPPORT Copy 2 "])
			.ShouldBe("Sibling support Copy 3");
		FinanceName.NextCopy(new string('a', 128), []).Length.ShouldBe(128);
		var first = FinanceName.NextCopy(new string('a', 128), []);
		FinanceName.NextCopy(new string('a', 128), [first]).ShouldNotBe(first);
	}
}
