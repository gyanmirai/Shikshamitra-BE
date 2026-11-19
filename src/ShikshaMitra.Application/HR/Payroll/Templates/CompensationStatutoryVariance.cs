using System;
using System.Linq;
using ShikshaMitra.HR.Payroll.Statutory;

namespace ShikshaMitra.HR.Payroll.Templates;

internal static class CompensationStatutoryVariance
{
	public static bool IsExplained(SalaryTemplateCalculationDto previous, PayrollStatutoryApplication current,
		CompensationProtectionPolicy policy, decimal difference)
	{
		var saved = previous.StatutoryApplication;
		var oldPfEmployee = saved?.ProvidentFund.AppliedEmployeeAmount ?? previous.Deductions.Where(x => x.SemanticType == "EMPLOYEE_PROVIDENT_FUND").Sum(x => x.Amount);
		var oldEsiEmployee = saved?.EmployeeStateInsurance.AppliedEmployeeAmount ?? previous.EmployeeStateInsurance.EmployeeContribution;
		var oldPfEmployer = saved?.ProvidentFund.AppliedEmployerAmount ?? previous.EmployerContributions.Where(x => x.Code.StartsWith("EMPLOYER_PF")).Sum(x => x.Amount);
		var oldEsiEmployer = saved?.EmployeeStateInsurance.AppliedEmployerAmount ?? previous.EmployeeStateInsurance.EmployerContribution;
		var pf = current.ProvidentFund; var esi = current.EmployeeStateInsurance;
		var changed = saved == null ? current.Profile.VersionId.HasValue :
			saved.ProvidentFund.Apply != pf.Apply || saved.ProvidentFund.AppliedEmployerAmount != pf.AppliedEmployerAmount || saved.ProvidentFund.AppliedEmployeeAmount != pf.AppliedEmployeeAmount ||
			saved.EmployeeStateInsurance.Apply != esi.Apply || saved.EmployeeStateInsurance.AppliedEmployerAmount != esi.AppliedEmployerAmount || saved.EmployeeStateInsurance.AppliedEmployeeAmount != esi.AppliedEmployeeAmount;
		var referenceUnchanged = pf.CalculatedEmployeeAmount == (saved?.ProvidentFund.CalculatedEmployeeAmount ?? oldPfEmployee) &&
			pf.CalculatedEmployerAmount == (saved?.ProvidentFund.CalculatedEmployerAmount ?? oldPfEmployer) &&
			esi.CalculatedEmployeeAmount == (saved?.EmployeeStateInsurance.CalculatedEmployeeAmount ?? oldEsiEmployee) &&
			esi.CalculatedEmployerAmount == (saved?.EmployeeStateInsurance.CalculatedEmployerAmount ?? oldEsiEmployer);
		var delta = policy switch
		{
			CompensationProtectionPolicy.ProtectEmployerCost => pf.AppliedEmployerAmount + esi.AppliedEmployerAmount - oldPfEmployer - oldEsiEmployer,
			CompensationProtectionPolicy.ProtectTakeHome => oldPfEmployee + oldEsiEmployee - pf.AppliedEmployeeAmount - esi.AppliedEmployeeAmount,
			_ => 0m
		};
		return changed && referenceUnchanged && Math.Abs(delta) > .01m && Math.Abs(difference - delta) <= .01m;
	}
}
