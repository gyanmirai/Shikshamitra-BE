using System.Collections.Generic;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class BankTransferCollectionDetails_Tests
{
	[Theory]
	[InlineData("transactionId", "2026-10-20")]
	[InlineData("transactionReferenceNumber", "2026-10-20T00:00:00+05:30")]
	public void Entered_Transfer_Evidence_Survives_Storage_RoundTrip(string referenceKey, string transferDate)
	{
		var details = FeeCollectionManager.MapPaymentDetails(PaymentMethodEnum.BankTransfer, new()
		{
			[referenceKey] = "N123456789876", ["transferDate"] = transferDate,
			["accountHolderName"] = "Rahul Sharma", ["bankName"] = "Example bank", ["ifscCode"] = "EXAM0001234",
		});
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		var stored = JsonSerializer.Serialize<PaymentDetails>(details!, options);
		var reloaded = JsonSerializer.Deserialize<PaymentDetails>(stored, options);
		var json = JsonSerializer.SerializeToElement<PaymentDetails>(reloaded!, options);
		json.GetProperty("transactionReferenceNumber").GetString().ShouldBe("N123456789876");
		json.TryGetProperty("transferDate", out var date).ShouldBeTrue();
		date.GetString().ShouldBe("2026-10-20");
		json.TryGetProperty("accountHolderName", out var holder).ShouldBeTrue();
		holder.GetString().ShouldBe("Rahul Sharma");
		json.GetProperty("bankName").GetString().ShouldBe("Example bank");
		json.GetProperty("ifscCode").GetString().ShouldBe("EXAM0001234");
	}

	[Fact]
	public void Canonical_Reference_Takes_Precedence_Over_Legacy_Alias()
	{
		var details = FeeCollectionManager.MapPaymentDetails(PaymentMethodEnum.BankTransfer, new()
		{
			["transactionReferenceNumber"] = "CANONICAL123", ["transactionId"] = "LEGACY123",
		}).ShouldBeOfType<BankTransferDetails>();
		details.TransactionReferenceNumber.ShouldBe("CANONICAL123");
	}

	[Fact]
	public void Historical_Json_Does_Not_Invent_Missing_Transfer_Evidence()
	{
		const string json = """{"$type":"BankTransferDetails","transactionReferenceNumber":"OLD12345","bankName":"Example bank","ifscCode":null}""";
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		var details = JsonSerializer.Deserialize<PaymentDetails>(json, options).ShouldBeOfType<BankTransferDetails>();
		details.TransactionReferenceNumber.ShouldBe("OLD12345");
		var serialized = JsonSerializer.SerializeToElement<PaymentDetails>(details, options);
		serialized.TryGetProperty("transferDate", out var date).ShouldBeTrue();
		date.ValueKind.ShouldBe(JsonValueKind.Null);
		serialized.TryGetProperty("accountHolderName", out var holder).ShouldBeTrue();
		holder.ValueKind.ShouldBe(JsonValueKind.Null);
	}
}
