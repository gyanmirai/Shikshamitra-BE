using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System.Text.Json;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class PaymentTermEnumJsonContract_Tests
{
	[Theory]
	[InlineData(DemandGenerationRule.OnInstallmentStartDate, "\"OnInstallmentStartDate\"")]
	[InlineData(DemandGenerationRule.XDaysBeforeDue, "\"XDaysBeforeDue\"")]
	[InlineData(DemandGenerationRule.OnDueDate, "\"OnDueDate\"")]
	[InlineData(DemandGenerationRule.SpecificDate, "\"SpecificDate\"")]
	public void DemandGenerationRule_Serializes_As_Its_Name(
		DemandGenerationRule rule,
		string expectedJson)
	{
		Assert.Equal(expectedJson, JsonSerializer.Serialize(rule));
	}

	[Fact]
	public void PaymentTerm_Response_Enums_Serialize_As_Names()
	{
		var responseEnums = new
		{
			Status = PaymentTermLifecycleStatus.Draft,
			VersionStatus = PaymentTermVersionStatus.Published,
			HolidayShiftingMethod = HolidayShiftingMethod.None,
			InitialDemandStatus = InitialDemandStatus.Published
		};

		var json = JsonSerializer.Serialize(responseEnums);

		Assert.Contains("\"Status\":\"Draft\"", json);
		Assert.Contains("\"VersionStatus\":\"Published\"", json);
		Assert.Contains("\"HolidayShiftingMethod\":\"None\"", json);
		Assert.Contains("\"InitialDemandStatus\":\"Published\"", json);
	}
}
