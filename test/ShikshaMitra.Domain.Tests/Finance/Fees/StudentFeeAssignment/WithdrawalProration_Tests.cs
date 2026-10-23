using System;
using System.Linq;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class WithdrawalProration_Tests
{
	[Fact]
	public void August_joining_and_December_withdrawal_recalculate_original_annual_basis_once()
	{
		var calculator = new ProrationPolicyCalculationService();
		var rule = new ProrationPolicyRuleSnapshot(Guid.NewGuid(), ProrationRuleType.FullMonthCharge,
			ProrationRoundingMode.RoundUpRupee, null, null, null, true, true, ProrationRuleType.FullMonthCharge);
		var admission = AssignmentAmountCalculator.Calculate(calculator, rule, 10000m, new(2026, 6, 1), new(2027, 3, 31), new(2026, 8, 1), new(2027, 3, 31));
		var withdrawal = AssignmentAmountCalculator.Calculate(calculator, rule, 10000m, new(2026, 6, 1), new(2027, 3, 31), new(2026, 8, 1), new(2026, 12, 31));
		Assert.Equal(8000m, admission.ProratedAmount);
		Assert.Equal(5000m, withdrawal.ProratedAmount);
		var credit = WithdrawalProrationAllocation.Credits([8000m], [0m], [100m], withdrawal.ProratedAmount).Single();
		var due = Due(8000m); due.ApplyPayment(6000m);
		var source = Guid.NewGuid(); due.ApplyWithdrawalProration(source, credit, withdrawal.Explanation);
		Assert.Equal(8000m, due.OriginalAmount); Assert.Equal(6000m, due.PaidAmount);
		Assert.Equal(3000m, due.AdjustmentCreditAmount); Assert.Equal(0m, due.ConcessionAmount);
		Assert.Equal(0m, due.OutstandingAmount); Assert.Equal(1000m, due.OverpaymentAmount);
		Assert.Equal(0m, due.RefundedAmount);
		due.ApplyWithdrawalProration(source, credit, withdrawal.Explanation);
		Assert.Equal(3000m, due.GetWithdrawalProrationCredit());
		due.ReverseWithdrawalProration(source);
		Assert.Equal(0m, due.GetWithdrawalProrationCredit()); Assert.Equal(2000m, due.OutstandingAmount);
		Assert.Equal(6000m, due.PaidAmount); Assert.Null(due.GetWithdrawalProrationSource());
	}

	[Fact]
	public void Rounding_and_existing_concessions_never_over_credit_or_increase_installment_principal()
	{
		for (var original = 1m; original <= 100m; original++)
		{
			var lines = AssignmentAmountCalculator.Allocate(original, [25m, 25m, 25m, 25m]);
			for (var revised = 0m; revised <= original; revised++)
			{
				var other = lines.Select(x => Math.Min(1m, x)).ToArray();
				var credits = WithdrawalProrationAllocation.Credits(lines, other, [25m, 25m, 25m, 25m], revised);
				Assert.Equal(Math.Min(original - revised, original - other.Sum()), credits.Sum());
				for (var i = 0; i < lines.Length; i++) Assert.InRange(credits[i], 0m, lines[i] - other[i]);
			}
		}
	}

	[Fact]
	public void Another_source_overcredit_and_wrong_correction_are_rejected_without_changing_paid_money()
	{
		var due = Due(100m); due.ApplyPayment(80m); due.ApplyConcession(10m);
		Assert.Throws<BusinessException>(() => due.ApplyWithdrawalProration(Guid.NewGuid(), 91m, "Invalid overlap"));
		var source = Guid.NewGuid(); due.ApplyWithdrawalProration(source, 50m, "Verified departure");
		Assert.Throws<BusinessException>(() => due.ApplyWithdrawalProration(Guid.NewGuid(), 1m, "Duplicate departure"));
		Assert.Throws<BusinessException>(() => due.ReverseWithdrawalProration(Guid.NewGuid()));
		Assert.Equal(80m, due.PaidAmount); Assert.Equal(10m, due.ConcessionAmount);
		Assert.Equal(40m, due.OverpaymentAmount); Assert.Equal(50m, due.GetWithdrawalProrationCredit());
	}

	[Fact]
	public void Head_credit_clears_unpaid_installments_before_creating_excess_on_paid_lines()
	{
		var credits = WithdrawalProrationAllocation.Credits([2000m, 2000m, 2000m, 2000m], [0m, 0m, 0m, 0m],
			[25m, 25m, 25m, 25m], 5000m, [2000m, 2000m, 2000m, 0m]);
		Assert.Equal(new[] { 0m, 0m, 1000m, 2000m }, credits);
	}

	[Fact]
	public void Daily_policy_uses_inclusive_dates_and_keeps_full_year_unchanged()
	{
		var calculator = new ProrationPolicyCalculationService();
		var rule = new ProrationPolicyRuleSnapshot(Guid.NewGuid(), ProrationRuleType.Daily,
			ProrationRoundingMode.RoundUpRupee, 0, null, null, true, true, ProrationRuleType.Daily);
		Assert.Equal(3040m, calculator.Calculate(rule, 3040m, new(2026, 6, 1), new(2027, 3, 31), new(2026, 6, 1), new(2027, 3, 31)).ProratedAmount);
		Assert.Equal(10m, calculator.Calculate(rule, 3040m, new(2026, 6, 1), new(2027, 3, 31), new(2026, 6, 2), new(2026, 6, 2)).ProratedAmount);
	}

	private static DueFee Due(decimal amount) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MonthEnum.June, 2026, false, amount, new(2026, 6, 10));
}
