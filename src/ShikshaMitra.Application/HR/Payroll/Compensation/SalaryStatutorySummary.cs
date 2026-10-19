using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;

namespace ShikshaMitra.HR.Payroll.Compensation;

internal static class SalaryStatutorySummary
{
	public static SalaryStatutorySummaryDto Pf(PayrollSchemeApplication? application, PfEligibilityDecision? eligibility, ProvidentFund? rule) => new()
	{
		Scheme = "PF",
		Status = application is { Apply: false } ? "TURNED_OFF" : eligibility?.Status == "NOT_APPLICABLE" ? "NOT_APPLICABLE" : rule == null || eligibility?.CanSave == false ? "NEEDS_SETUP" : "APPLIED",
		Reason = application is { Apply: false } ? "PF is turned off for this staff member." : eligibility?.Status == "NOT_APPLICABLE" ? "School PF coverage does not apply." : rule == null ? "Choose an effective PF rule." : eligibility?.CanSave == false ? "Review PF membership and enrollment." : "Employee and employer contributions use the school PF rule.",
		RegistrationPending = application is { Apply: true, NumberPresent: false },
		EmployeeAmount = application?.AppliedEmployeeAmount ?? 0m,
		EmployerAmount = application?.AppliedEmployerAmount ?? 0m,
		ContributionWages = application?.ContributionWages ?? eligibility?.Wages,
		EmployeePercentage = rule?.EmployeePercentage,
		EmployerPercentage = rule?.EmployerPercentage,
		RuleId = rule?.Id,
		RuleVersion = rule?.Version
	};

	public static SalaryStatutorySummaryDto Esi(EsiStatutoryCalculationResult? calculation, PayrollSchemeApplication? application, bool enabled) => new()
	{
		Scheme = "ESIC",
		Status = !enabled || application is { Apply: false } ? "TURNED_OFF" : calculation == null || !calculation.CanCalculate ? "NEEDS_SETUP" : !calculation.IsCovered ? "NOT_APPLICABLE" : "APPLIED",
		Reason = !enabled ? "ESIC calculation is turned off in school settings." : application is { Apply: false } ? "ESIC is turned off for this staff member." : calculation == null ? "No effective ESIC rule is available for this month." : !calculation.CanCalculate ? "Complete ESIC configuration and earning classifications." : !calculation.IsCovered ? "Outside ESIC coverage for this month." : calculation.CoverageDecision == "CONTRIBUTION_PERIOD_CONTINUATION" ? "Coverage continues until the contribution period ends." : calculation.EmployeeContribution == 0m && calculation.EmployerContribution > 0m ? "Employee share is exempt; the employer share still applies." : "Covered under the school ESIC rules for this month.",
		RegistrationPending = application is { Apply: true, NumberPresent: false },
		EmployeeAmount = calculation?.CanCalculate == true ? calculation.EmployeeContribution : enabled ? null : 0m,
		EmployerAmount = calculation?.CanCalculate == true ? calculation.EmployerContribution : enabled ? null : 0m,
		ContributionWages = calculation?.StatutoryWages,
		EmployeePercentage = calculation?.EmployeePercentage,
		EmployerPercentage = calculation?.EmployerPercentage
	};

	public static SalaryStatutorySummaryDto ProfessionalTax(bool included, ProfessionalTax? slab, bool cessationExemption = false) => new()
	{
		Scheme = "PT",
		Status = !included || cessationExemption ? "NOT_APPLICABLE" : slab == null ? "NEEDS_SETUP" : slab.TaxAmount > 0m ? "APPLIED" : "NOT_APPLICABLE",
		Reason = !included ? "Professional tax is not included in this salary structure." : cessationExemption ? "Professional tax is exempt in the employment cessation month under the effective slab." : slab == null ? "No matching professional-tax slab is available for this month." : slab.TaxAmount > 0m ? "Applied using the effective Gujarat professional-tax slab." : "No professional tax is payable in this salary slab.",
		EmployeeAmount = !included || cessationExemption ? 0m : slab?.TaxAmount,
		EmployerAmount = 0m,
		RuleId = slab?.Id,
		RuleVersion = slab?.Version
	};
}
