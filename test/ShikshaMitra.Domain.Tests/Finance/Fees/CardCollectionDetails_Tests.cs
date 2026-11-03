using System.Text.Json;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class CardCollectionDetails_Tests
{
	[Theory]
	[InlineData(PaymentMethodEnum.CreditCard)]
	[InlineData(PaymentMethodEnum.DebitCard)]
	public void Entered_Card_Evidence_Survives_Storage_RoundTrip(PaymentMethodEnum method)
	{
		var details = FeeCollectionManager.MapPaymentDetails(method, new()
		{
			["transactionId"] = "CARD123456", ["last4Digits"] = "0123",
			["cardHolderName"] = "Rahul Sharma", ["bankName"] = "Example bank",
		});
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		var stored = JsonSerializer.Serialize<PaymentDetails>(details!, options);
		var reloaded = JsonSerializer.Deserialize<PaymentDetails>(stored, options);
		var json = JsonSerializer.SerializeToElement<PaymentDetails>(reloaded!, options);
		json.GetProperty("cardLast4").GetString().ShouldBe("0123");
		json.TryGetProperty("cardHolderName", out var holder).ShouldBeTrue();
		holder.GetString().ShouldBe("Rahul Sharma");
		json.GetProperty("transactionId").GetString().ShouldBe("CARD123456");
	}

	[Fact]
	public void Canonical_Last_Four_Takes_Precedence_Over_Form_Alias()
	{
		var details = FeeCollectionManager.MapPaymentDetails(PaymentMethodEnum.CreditCard, new()
		{
			["cardLast4"] = "4321", ["last4Digits"] = "0123",
		}).ShouldBeOfType<CreditCardDetails>();
		details.CardLast4.ShouldBe("4321");
	}

	[Fact]
	public void Historical_Card_Json_Does_Not_Invent_A_Holder()
	{
		const string json = """{"$type":"CreditCardDetails","transactionId":"OLD12345","cardLast4":"0123","bankName":"Example bank"}""";
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		var details = JsonSerializer.Deserialize<PaymentDetails>(json, options);
		var serialized = JsonSerializer.SerializeToElement<PaymentDetails>(details!, options);
		serialized.GetProperty("cardLast4").GetString().ShouldBe("0123");
		serialized.TryGetProperty("cardHolderName", out var holder).ShouldBeTrue();
		holder.ValueKind.ShouldBe(JsonValueKind.Null);
	}
}
