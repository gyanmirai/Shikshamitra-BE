using System.Linq;
using ShikshaMitra.HR.Payroll.Statutory;

namespace ShikshaMitra.HR.Payroll.Templates;

internal static class SalaryTemplateStatutoryApplication
{
	public static SalaryTemplateEvaluation Apply(SalaryTemplateEvaluation evaluation, PayrollStatutoryProfile profile, bool pfEnrolled)
	{
		var pfEmployee = evaluation.Deductions.Where(x => x.SemanticType == "EMPLOYEE_PROVIDENT_FUND").Sum(x => x.Amount);
		var pfEmployer = evaluation.EmployerContributions.Where(x => x.Code.StartsWith("EMPLOYER_PF")).Sum(x => x.Amount);
		var pf = PayrollSchemeApplication.Decide("PF", profile.ApplyPf, profile.PfNumber, pfEnrolled, pfEmployee, pfEmployer, profile.PfDisabledReason);
		var calculation = evaluation.EmployeeStateInsurance;
		var esi = PayrollSchemeApplication.Decide("ESI", profile.ApplyEsic, profile.EsicInsuranceNumber, calculation?.IsCovered ?? false,
			calculation?.EmployeeContribution ?? 0m, calculation?.EmployerContribution ?? 0m, profile.EsicDisabledReason);
		var deductions = evaluation.Deductions.Where(x =>
			(x.SemanticType != "EMPLOYEE_PROVIDENT_FUND" || pf.AppliedEmployeeAmount > 0m) &&
			(x.SemanticType != "EMPLOYEE_STATE_INSURANCE" || esi.AppliedEmployeeAmount > 0m)).ToList();
		var employer = evaluation.EmployerContributions.Where(x =>
			(!x.Code.StartsWith("EMPLOYER_PF") || pf.AppliedEmployerAmount > 0m) &&
			(!x.Code.StartsWith("EMPLOYER_ESI") || esi.AppliedEmployerAmount > 0m)).ToList();
		if (calculation != null)
			calculation = calculation with { Application = esi, EmployeeContribution = esi.AppliedEmployeeAmount,
				EmployerContribution = esi.AppliedEmployerAmount,
				GovernmentContribution = esi.Apply && esi.Eligible ? calculation.GovernmentContribution : 0m,
				IssueCodes = calculation.IssueCodes.Where(x => x != EsiStatutoryCalculator.InsuranceNumberIssue)
					.Concat(esi.WarningCode == null ? [] : new[] { esi.WarningCode }).ToList() };
		var total = deductions.Sum(x => x.Amount);
		return evaluation with { Deductions = deductions, EmployerContributions = employer, TotalDeductions = total,
			Net = evaluation.Gross - total, EmployerContributionTotal = employer.Sum(x => x.Amount),
			EmployeeStateInsurance = calculation, StatutoryApplication = new(profile, pf, esi) };
	}
}
