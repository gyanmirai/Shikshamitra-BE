using System;
using System.Linq;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Fees;

public class ConcessionEligibility_Tests
{
	[Fact]
	public void Current_and_future_use_canonical_installment_cohorts()
	{
		var studentId = Guid.NewGuid();
		var termVersionId = Guid.NewGuid();
		var firstInstallmentId = Guid.NewGuid();
		var secondInstallmentId = Guid.NewGuid();
		var firstHead = BuildDue(
			studentId, Guid.NewGuid(), MonthEnum.January, termVersionId, firstInstallmentId, 1);
		var anotherHeadInFirst = BuildDue(
			studentId, Guid.NewGuid(), MonthEnum.February, termVersionId, firstInstallmentId, 1);
		var second = BuildDue(
			studentId, Guid.NewGuid(), MonthEnum.March, termVersionId, secondInstallmentId, 2);

		var all = new[] { second, anotherHeadInFirst, firstHead }.ToList();
		var current = ConcessionRequestManager.GetEligibleDueFees(all, ConcessionApplyOn.CurrentInstallment);
		var future = ConcessionRequestManager.GetEligibleDueFees(all, ConcessionApplyOn.FutureInstallments);

		Assert.Equal(2, current.Count);
		Assert.All(current, due => Assert.Equal(firstInstallmentId, due.PaymentTermInstallmentId));
		Assert.Single(future);
		Assert.Equal(secondInstallmentId, future[0].PaymentTermInstallmentId);
	}

	[Fact]
	public void All_fees_includes_paid_installments_for_retroactive_concessions()
	{
		var paid = BuildDue(Guid.NewGuid(), Guid.NewGuid(), MonthEnum.January,
			Guid.NewGuid(), Guid.NewGuid(), 1);
		paid.ApplyPayment(100m);

		Assert.Empty(ConcessionRequestManager.GetEligibleDueFees([paid], ConcessionApplyOn.CurrentInstallment));
		Assert.Single(ConcessionRequestManager.GetEligibleDueFees([paid], ConcessionApplyOn.AllFees));
	}

	private static DueFee BuildDue(
		Guid studentId,
		Guid feeHeadId,
		MonthEnum month,
		Guid termVersionId,
		Guid installmentId,
		int sequence) => new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			studentId,
			feeHeadId,
			month,
			2026,
			false,
			100m,
			DateTime.UtcNow.AddMonths(sequence),
			paymentTermVersionId: termVersionId,
			paymentTermInstallmentId: installmentId,
			installmentSequence: sequence,
			installmentName: $"Installment {sequence}");
}
