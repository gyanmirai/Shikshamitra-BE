using System;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.Statutory;

public static class EsiPayrollReadiness
{
	public static bool IsCovered(Guid? ruleId, string? decision) => ruleId.HasValue &&
		decision is "WITHIN_WAGE_CEILING" or "CONTRIBUTION_PERIOD_CONTINUATION" or "FORCE_COVERED_OVERRIDE";

	public static bool MissingInsuranceNumber(Guid? ruleId, string? decision, string? number, string? applicationJson = null)
	{
		if (applicationJson != null && JsonSerializer.Deserialize<PayrollStatutoryApplication>(applicationJson) is { } application &&
			(!application.EmployeeStateInsurance.Apply || !application.EmployeeStateInsurance.NumberPresent) &&
			application.EmployeeStateInsurance.AppliedEmployeeAmount == 0m && application.EmployeeStateInsurance.AppliedEmployerAmount == 0m)
			return false;
		return IsCovered(ruleId, decision) && !PayrollSchemeApplication.HasNumber(number);
	}

	public static void EnsureInsuranceNumber(Guid? ruleId, string? decision, string? number, string? applicationJson = null)
	{
		if (MissingInsuranceNumber(ruleId, decision, number, applicationJson))
			throw new BusinessException(HRErrorCodes.PayrollBlockingIssuesBeforeLock)
				.WithData("issueCodes", new[] { EsiStatutoryCalculator.InsuranceNumberIssue });
	}
}