using System;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Statutory;

public static class EsiPayrollReadiness
{
	public static bool IsCovered(Guid? ruleId, string? decision) => ruleId.HasValue &&
		decision is "WITHIN_WAGE_CEILING" or "CONTRIBUTION_PERIOD_CONTINUATION" or "FORCE_COVERED_OVERRIDE";

	public static bool MissingInsuranceNumber(Guid? ruleId, string? decision, string? number) =>
		IsCovered(ruleId, decision) && string.IsNullOrWhiteSpace(number);

	public static void EnsureInsuranceNumber(Guid? ruleId, string? decision, string? number)
	{
		if (MissingInsuranceNumber(ruleId, decision, number))
			throw new BusinessException(HRErrorCodes.PayrollBlockingIssuesBeforeLock)
				.WithData("issueCodes", new[] { EsiStatutoryCalculator.InsuranceNumberIssue });
	}
}