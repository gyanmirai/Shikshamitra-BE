using System.Globalization;
using System.Linq;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalEvidenceJson_Tests
{
	[Fact]
	public void Sql_Reason_Whitespace_Set_Should_Match_The_Actual_DotNet_Whitespace_Contract()
	{
		var expected = Enumerable.Range(0, char.MaxValue + 1).Select(value => (char)value)
			.Where(char.IsWhiteSpace).ToArray();
		FinanceExternalEvidenceJson.WhiteSpaceCharacters.Distinct().Order().ToArray().ShouldBe(expected);
	}

	[Theory]
	[InlineData("{\"Payer\":{\"Id\":null}}", "Payer.Id", "null", "null")]
	[InlineData("{\"Payer\":{}}", "Payer.Id", "null", null)]
	[InlineData("{\"Payer\":{\"Id\":42}}", "Payer.Id", "string", null)]
	[InlineData("{\"Reason\":\"Maya José / ગુજરાતી\"}", "Reason", "string", "Maya José / ગુજરાતી")]
	[InlineData("{\"Ready\":true}", "Ready", "boolean", "true")]
	[InlineData("{\"Payer\":{\"Id\":\"a\",\"Id\":\"b\"}}", "Payer.Id", "string", null)]
	[InlineData("{\"Payer\":{\"Id\":\"a\"},\"Payer\":{\"Id\":\"b\"}}", "Payer.Id", "string", null)]
	[InlineData("{\"Reason\":\"\\u0000\"}", "Reason", "string", null)]
	[InlineData("{bad", "Reason", "string", null)]
	[InlineData("[]", "Reason", "string", null)]
	[InlineData(null, "Reason", "string", null)]
	[InlineData("{}", "", "string", null)]
	public void Scalar_Should_Require_Exact_Path_Type_And_Presence(string? document, string path,
		string expectedKind, string? expected)
	{
		FinanceExternalEvidenceJson.Scalar(document, path, expectedKind).ShouldBe(expected);
	}

	[Theory]
	[InlineData("{\"Amount\":9999999999999999.99}", "9999999999999999.99")]
	[InlineData("{\"Amount\":12.5000}", "12.50")]
	[InlineData("{\"Amount\":0}", "0.00")]
	[InlineData("{\"Amount\":0.0000000000000000000000000001}", null)]
	[InlineData("{\"Amount\":10000000000000000}", null)]
	[InlineData("{\"Amount\":-1}", null)]
	[InlineData("{\"Amount\":1e1}", null)]
	[InlineData("{\"Amount\":\"100.75\"}", null)]
	[InlineData("{\"Amount\":100.75,\"Amount\":1}", null)]
	public void Money_Should_Preserve_Exact_Whole_Paise_Without_Implicit_Rounding(string document, string? expected)
	{
		FinanceExternalEvidenceJson.Money(document, "Amount")?.ToString("0.00", CultureInfo.InvariantCulture)
			.ShouldBe(expected);
	}
}
