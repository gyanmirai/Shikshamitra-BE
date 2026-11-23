using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.Domain.Finance.Policies;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public static class MonthlyFeePricing
{
	public static bool IsMonthlySchedule(IReadOnlyList<ResolvedInstallment> installments) =>
		installments.Count > 0 &&
		installments.All(row => MonthKey(row.CoverageStartDate) == MonthKey(row.CoverageEndDate)) &&
		installments.Select(row => MonthKey(row.CoverageStartDate)).Distinct().Count() == installments.Count;

	public static decimal AmountPerInstallment(decimal rate) => WholeRupeeAmounts.RoundUp(rate);

	public static ProrationCalculationResult Calculate(
		ProrationPolicyCalculationService calculator,
		ProrationPolicyRuleSnapshot rule,
		decimal rate,
		DateTime coverageStart,
		DateTime coverageEnd,
		DateTime serviceStart,
		DateTime serviceEnd)
	{
		// Legacy monthly cutoff governs admission; legacy withdrawal uses the full service month.
		if (rule.RuleType == ProrationRuleType.MonthlyCutoff && rule.EarlyWithdrawalRuleType == null)
			rule = rule with { EarlyWithdrawalRuleType = ProrationRuleType.FullMonthCharge };
		return calculator.Calculate(rule, AmountPerInstallment(rate), coverageStart, coverageEnd, serviceStart, serviceEnd);
	}

	private static int MonthKey(DateTime date) => date.Year * 12 + date.Month;
}
