using System;
using System.Text.Json;

namespace ShikshaMitra.HR.Payroll.Statutory;

public static class EsiPayrollReadiness
{
	public static bool IsCovered(Guid? ruleId, string? decision) => ruleId.HasValue &&
		decision is "WITHIN_WAGE_CEILING" or "CONTRIBUTION_PERIOD_CONTINUATION" or "FORCE_COVERED_OVERRIDE";

	public static bool MissingInsuranceNumber(Guid? ruleId, string? decision, string? number, string? applicationJson = null)
	{
		if (applicationJson != null && JsonSerializer.Deserialize<PayrollStatutoryApplication>(applicationJson) is { } application &&
			!application.EmployeeStateInsurance.Apply)
			return false;
		return IsCovered(ruleId, decision) && !PayrollSchemeApplication.HasNumber(number);
	}

}