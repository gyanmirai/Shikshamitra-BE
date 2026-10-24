using System;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class DueFee_AppliedPayment_Tests
{
	private static DueFee CreateDue() => new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
		MonthEnum.June, 2026, false, 50_000m, new DateTime(2026, 6, 10));

	[Fact]
	public void Reported_case_separates_applied_principal_from_excess_without_rewriting_cash()
	{
		var due = CreateDue();
		due.ApplyPayment(50_000m);
		due.ApplyConcession(37_500m);
		due.ApplyPayment(12_500m);

		Assert.Equal(12_500m, due.NetPayableAmount);
		Assert.Equal(12_500m, due.AppliedPaymentAmount);
		Assert.Equal(50_000m, due.OverpaymentAmount);
		Assert.Equal(62_500m, due.PaidAmount);
		Assert.Equal(due.PaidAmount - due.RefundedAmount, due.AppliedPaymentAmount + due.OverpaymentAmount);
	}

	[Fact]
	public void Concession_alone_does_not_create_a_payment()
	{
		var due = CreateDue();
		due.ApplyConcession(37_500m);
		Assert.Equal(0m, due.AppliedPaymentAmount);
		Assert.Equal(12_500m, due.OutstandingAmount);
	}

	[Fact]
	public void Refunds_reduce_excess_first_and_then_reopen_principal()
	{
		var due = CreateDue();
		due.ApplyConcession(37_500m);
		due.ApplyPayment(62_500m);
		due.ApplyRefund(50_000m);
		Assert.Equal(12_500m, due.AppliedPaymentAmount);
		Assert.Equal(0m, due.OverpaymentAmount);
		due.ApplyRefund(2_500m);
		Assert.Equal(10_000m, due.AppliedPaymentAmount);
		Assert.Equal(2_500m, due.OutstandingAmount);
		due.ReverseRefund(2_500m);
		Assert.Equal(12_500m, due.AppliedPaymentAmount);
	}

	[Fact]
	public void Credits_and_payment_reversals_preserve_the_balance_equation()
	{
		var due = CreateDue();
		due.ApplyConcession(37_500m);
		due.ApplyAdjustmentCredit(2_500m);
		due.ApplyPayment(10_000m);
		due.ReversePayment(1_000m);
		Assert.Equal(10_000m, due.NetPayableAmount);
		Assert.Equal(9_000m, due.AppliedPaymentAmount);
		Assert.Equal(1_000m, due.OutstandingAmount);
		due.ReverseConcession(2_500m);
		Assert.Equal(12_500m, due.NetPayableAmount);
		Assert.Equal(due.OriginalAmount, due.ConcessionAmount + due.AdjustmentCreditAmount + due.AppliedPaymentAmount + due.OutstandingAmount);
	}
}
