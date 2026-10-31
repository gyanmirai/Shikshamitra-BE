using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public class PendingFeeRefundProjection_Tests
{
	[Fact]
	public void Previous_processed_refund_is_separate_from_current_approved_unpaid_refund()
	{
		var dueId = Guid.NewGuid();
		var historical = Create(dueId, 15000m, "REF-HISTORICAL");
		Approve(historical);
		Pay(historical);
		var current = Create(dueId, 3750m, "REF-CURRENT");
		Approve(current);

		var result = Project([historical, current]);

		Assert.Equal(3750m, result[dueId].Amount);
		Assert.Contains("REF-CURRENT", result[dueId].Description);
		Assert.DoesNotContain("REF-HISTORICAL", result[dueId].Description);
		Assert.Equal(15000m, historical.ProcessedAmount);
		Assert.Equal(0m, current.ProcessedAmount);
	}

	[Fact]
	public void Multiple_requests_group_by_due_and_use_approved_amount_instead_of_requested_amount()
	{
		var dueId = Guid.NewGuid();
		var first = Create(dueId, 1000m, "REF-CAPPED", calculatedAmount: 200m);
		Approve(first);
		var second = Create(dueId, 200m, "REF-SECOND", calculatedAmount: 150m);
		Approve(second);
		var other = Create(Guid.NewGuid(), 80m, "REF-OTHER");
		Approve(other);

		var result = Project([first, second, other]);

		Assert.Equal(350m, result[dueId].Amount);
		Assert.Equal(2, result.Count);
		Assert.Contains("REF-CAPPED", result[dueId].Description);
		Assert.Contains("REF-SECOND", result[dueId].Description);
		Assert.DoesNotContain("REF-OTHER", result[dueId].Description);
	}

	[Fact]
	public void Approved_amount_stays_pending_until_payout_confirmation_then_disappears()
	{
		var dueId = Guid.NewGuid();
		var request = Create(dueId, 3750m, "REF-VERIFY");
		Assert.Empty(Project([request]));
		Approve(request);
		Assert.Equal(3750m, Project([request])[dueId].Amount);
		var payout = request.RecordPayout(Guid.NewGuid(), Guid.NewGuid(), "attempt", "Cash", 3750m,
			DateTimeOffset.UtcNow, null, null, null, "handover evidence");
		Assert.Equal(3750m, Project([request])[dueId].Amount);
		request.ConfirmPayout(payout.Id, Guid.NewGuid(), "confirmed", "confirmation evidence");
		Assert.Empty(Project([request]));
	}

	[Fact]
	public void Failed_payout_and_its_replacement_do_not_double_count_the_old_approval()
	{
		var dueId = Guid.NewGuid();
		var failed = Create(dueId, 3750m, "REF-FAILED");
		Approve(failed);
		var payout = failed.RecordPayout(Guid.NewGuid(), Guid.NewGuid(), "attempt", "Cash", 3750m,
			DateTimeOffset.UtcNow, null, null, null, "attempt evidence");
		failed.FailPayout(payout.Id, Guid.NewGuid(), "failed", "Cash was not delivered", "failure evidence");
		var replacement = Create(dueId, 3750m, "REF-REPLACEMENT");
		Approve(replacement);

		Assert.Equal(3750m, Project([failed, replacement])[dueId].Amount);
		Assert.DoesNotContain("REF-FAILED", Project([failed, replacement])[dueId].Description);
	}

	[Fact]
	public void Returned_processed_payout_does_not_become_a_pending_approval_again()
	{
		var request = Create(Guid.NewGuid(), 3750m, "REF-RETURNED", "UPI");
		Approve(request);
		var payout = Pay(request);
		request.RecordReturnedPayout(payout.Id, Guid.NewGuid(), "Bank returned this payout");
		Assert.Empty(Project([request]));
		Assert.Equal(3750m, request.ReversedAmount);
	}

	[Theory]
	[InlineData("rejected")]
	[InlineData("cancelled")]
	[InlineData("reversed")]
	[InlineData("legacyApproved")]
	public void Terminal_or_legacy_approvals_do_not_appear_as_unpaid_workflow_one_refunds(string transition)
	{
		var request = Create(Guid.NewGuid(), 500m, "REF-EXCLUDED", simple: transition != "legacyApproved");
		if (transition == "rejected") request.Reject(Guid.NewGuid(), "Request was rejected");
		else if (transition == "cancelled")
		{
			Approve(request);
			request.Cancel(Guid.NewGuid(), "Request was cancelled");
		}
		else if (transition == "reversed")
		{
			Approve(request);
			var payout = Pay(request);
			request.Reverse(payout.Id, Guid.NewGuid(), "Refund was returned");
		}
		else Approve(request);
		Assert.Empty(Project([request]));
	}

	[Fact]
	public void Processing_approval_is_pending_but_paid_charge_and_late_fee_sources_are_not_principal()
	{
		var dueId = Guid.NewGuid();
		var request = Create(dueId, 500m, "REF-PROCESSING");
		Approve(request);
		request.StartProcessing(Guid.NewGuid(), Guid.NewGuid(), "processing", "Cash", 500m,
			DateTimeOffset.UtcNow, null, null);
		Assert.Equal(500m, Project([request])[dueId].Amount);

		var charges = Create(null, 100m, "REF-CHARGE", chargeId: Guid.NewGuid());
		Approve(charges);
		var late = Create(null, 50m, "REF-LATE", lateFeeDueFeeId: dueId);
		Approve(late);
		Assert.Equal(500m, Project([request, charges, late])[dueId].Amount);
		Assert.Single(Project([request, charges, late]));
	}

	private static Dictionary<Guid, (decimal Amount, string Description)> Project(IEnumerable<FeeRefundRequest> requests)
		=> PendingFeeRefundProjection.Build(requests);

	private static FeeRefundRequest Create(Guid? dueId, decimal amount, string voucher, string method = "Cash",
		bool simple = true, Guid? chargeId = null, Guid? lateFeeDueFeeId = null, decimal? calculatedAmount = null)
	{
		var request = new FeeRefundRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			voucher, method, null, "Refund requested for paid fees");
		var receipt = Guid.NewGuid();
		request.PinPolicy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), receipt, "RECEIPT", "{}", "{}", new string('a', 64));
		request.AddLine(new FeeRefundLine(Guid.NewGuid(), request.Id, dueId, Guid.NewGuid(), receipt,
			dueId.HasValue ? Guid.NewGuid() : null, "Fee head", "December", amount, amount, calculatedAmount ?? amount,
			chargeId, lateFeeDueFeeId));
		if (simple) request.UseSimpleWorkflow(Guid.NewGuid(), voucher);
		else request.Submit(Guid.NewGuid());
		return request;
	}

	private static void Approve(FeeRefundRequest request)
	{
		var line = request.Lines.Single();
		request.Approve(Guid.NewGuid(), [new FeeRefundReservation(Guid.NewGuid(), request.Id, line.Id,
			line.DueFeeId, line.SourceAllocationId, line.CalculatedAmount, line.ChargeId)]);
	}

	private static FeeRefundSettlement Pay(FeeRefundRequest request)
	{
		var payout = request.RecordPayout(Guid.NewGuid(), Guid.NewGuid(), "payout", request.RefundMethod!,
			request.ApprovedAmount, DateTimeOffset.UtcNow, null, null, null, "handover evidence");
		request.ConfirmPayout(payout.Id, Guid.NewGuid(), "confirmed", "confirmation evidence");
		return payout;
	}
}
