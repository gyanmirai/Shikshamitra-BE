using System;
using System.Reflection;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Mobile;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance;

public class MobileRefundPolicyInput_Tests
{
	private static CreateFeeRefundRequestDto Map(string lines)
	{
		var input = JsonSerializer.Deserialize<MobileRefundInput>("{\"refundMethod\":\"Cash\",\"paymentDetails\":{\"receivedBy\":\"Parent\"},\"reason\":\"Refund paid items\",\"lines\":" + lines + "}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		input.OriginalReceiptId = Guid.NewGuid();
		return (CreateFeeRefundRequestDto)typeof(FeeRefundAppService).GetMethod("FamilyRefundInput", BindingFlags.NonPublic | BindingFlags.Static)!
			.Invoke(null, [Guid.NewGuid(), Guid.NewGuid(), input])!;
	}

	[Theory]
	[InlineData("chargeId")]
	[InlineData("lateFeeDueFeeId")]
	public void Parent_refund_preserves_the_selected_non_principal_source(string source)
	{
		var id = Guid.NewGuid(); var allocation = Guid.NewGuid();
		var line = Map($"[{{\"{source}\":\"{id}\",\"sourceAllocationId\":\"{allocation}\",\"amount\":\"500.00\"}}]").Lines.ShouldHaveSingleItem();
		line.DueFeeId.ShouldBeNull();
		(source == "chargeId" ? line.ChargeId : line.LateFeeDueFeeId).ShouldBe(id);
		line.SourceAllocationId.ShouldBe(allocation);
		line.Amount.ShouldBe(500m);
	}

	[Fact]
	public void Parent_refund_rejects_ambiguous_source_identifiers_before_preparing_money()
	{
		var exception = Should.Throw<TargetInvocationException>(() => Map($"[{{\"dueFeeId\":\"{Guid.NewGuid()}\",\"chargeId\":\"{Guid.NewGuid()}\",\"sourceAllocationId\":\"{Guid.NewGuid()}\",\"amount\":\"1.00\"}}]"));
		exception.InnerException.ShouldBeOfType<BusinessException>();
	}

	[Fact]
	public void Existing_due_only_commands_retain_their_serialization_for_durable_replay()
	{
		var id = Guid.NewGuid(); var allocation = Guid.NewGuid();
		var oldLine = new { DueFeeId = id, SourceAllocationId = allocation, Amount = "125.50" };
		var line = JsonSerializer.Deserialize<MobileRefundLineInput>(JsonSerializer.Serialize(oldLine))!;
		MobileFinanceCommandRules.Hash(line).ShouldBe(MobileFinanceCommandRules.Hash(oldLine));
	}
}
