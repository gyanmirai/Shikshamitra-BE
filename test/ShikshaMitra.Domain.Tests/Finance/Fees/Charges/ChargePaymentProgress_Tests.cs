using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Charges;

public class ChargePaymentProgress_Tests
{
	[Fact]
	public void Progress_uses_real_balances_and_does_not_count_partial_payments_as_paid()
	{
		var result = ChargePaymentProgress.Resolve([
			new(ChargeStatus.Approved, 500m, 500m, 0m, true),
			new(ChargeStatus.Approved, 500m, 200m, 0m, true),
			new(ChargeStatus.Approved, 500m, 0m, 500m),
			new(ChargeStatus.Approved, 500m, 0m, 100m),
			new(ChargeStatus.Pending, 500m, 0m, 0m),
			new(ChargeStatus.Rejected, 500m, 0m, 0m)
		]);
		result.PaidStudentCount.ShouldBe(1);
		result.OutstandingStudentCount.ShouldBe(2);
		result.WaivedStudentCount.ShouldBe(2);
		result.CanCancel.ShouldBeFalse();
	}

	[Fact]
	public void Reversal_and_waiver_reversal_reopen_outstanding_progress()
	{
		ChargePaymentProgress.Resolve([new(ChargeStatus.Approved, 500m, 500m, 0m, true)])
			.PaidStudentCount.ShouldBe(1);
		var reversed = ChargePaymentProgress.Resolve([new(ChargeStatus.Approved, 500m, 0m, 0m)]);
		reversed.PaidStudentCount.ShouldBe(0);
		reversed.OutstandingStudentCount.ShouldBe(1);
		reversed.CanCancel.ShouldBeTrue();
		reversed.WorkflowStatus.ShouldBe(ChargeWorkflowStatus.InProcess);
	}

	[Fact]
	public void Uncollected_waived_batches_can_cancel_but_active_allocations_cannot()
	{
		ChargePaymentProgress.Resolve([new(ChargeStatus.Approved, 500m, 0m, 500m)])
			.CanCancel.ShouldBeTrue();
		ChargePaymentProgress.Resolve([new(ChargeStatus.Approved, 500m, 0m, 0m, true)])
			.CanCancel.ShouldBeFalse();
		ChargePaymentProgress.Resolve([new(ChargeStatus.Pending, 500m, 0m, 0m)])
			.CanCancel.ShouldBeFalse();
	}
}
