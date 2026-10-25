using System;
using System.Collections.Generic;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class DemandDraftCollectionDates_Tests
{
	[Theory]
	[InlineData("2026-10-10", "2026-10-15")]
	[InlineData("2026-10-10T00:00:00+05:30", "2026-10-15T00:00:00+05:30")]
	public void Maps_Entered_Issue_And_Payment_Dates_Independently(string issueDate, string paymentDate)
	{
		var details = Map(new() { ["issueDate"] = issueDate, ["paymentDate"] = paymentDate,
			["bankName"] = "School bank" });
		details.DDDate.ShouldBe(new DateOnly(2026, 10, 10));
		details.PaymentDate.ShouldBe(new DateOnly(2026, 10, 15));
		details.IssuingBankName.ShouldBe("School bank");
		var roundTrip = JsonSerializer.Deserialize<PaymentDetails>(
			JsonSerializer.Serialize<PaymentDetails>(details, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
			new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldBeOfType<DemandDraftDetails>();
		roundTrip.DDDate.ShouldBe(details.DDDate);
		roundTrip.PaymentDate.ShouldBe(details.PaymentDate);
	}

	[Fact]
	public void Supports_Legacy_DdDate_And_Preserves_Missing_Dates()
	{
		Map(new() { ["ddDate"] = "2026-10-10" }).DDDate.ShouldBe(new DateOnly(2026, 10, 10));
		var missing = Map(new() { ["paymentDate"] = "2026-10-15" });
		missing.DDDate.ShouldBeNull();
		missing.PaymentDate.ShouldBe(new DateOnly(2026, 10, 15));
		Map(new()).PaymentDate.ShouldBeNull();
	}

	[Fact]
	public void Legacy_Json_Without_Payment_Date_Still_Loads()
	{
		const string json = """{"$type":"DemandDraftDetails","ddNumber":"123456","issuingBankName":"School bank","branchName":"City","ddDate":"2026-10-10"}""";
		var details = JsonSerializer.Deserialize<PaymentDetails>(json,
			new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldBeOfType<DemandDraftDetails>();
		details.DDDate.ShouldBe(new DateOnly(2026, 10, 10));
		details.PaymentDate.ShouldBeNull();
	}

	private static DemandDraftDetails Map(Dictionary<string, object?> details) =>
		FeeCollectionManager.MapPaymentDetails(PaymentMethodEnum.DemandDraft, details)
			.ShouldBeOfType<DemandDraftDetails>();
}
