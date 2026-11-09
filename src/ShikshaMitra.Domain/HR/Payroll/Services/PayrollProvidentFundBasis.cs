using System;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollProvidentFundBasis
{
	public static bool IsEligible(EmployeePayrollComponent component, PfContributionBasis basis) =>
		component.IsEarning && !component.IsTemp &&
		(component.SemanticType == PayrollComponentSemanticType.Basic || IsBasicName(component.Name) ||
		 basis == PfContributionBasis.BasicAndDearnessAllowance &&
		 component.SemanticType == PayrollComponentSemanticType.DearnessAllowance);

	private static bool IsBasicName(string? name) =>
		!string.IsNullOrWhiteSpace(name) &&
		(string.Equals(name, "Basic", StringComparison.OrdinalIgnoreCase) ||
		 name.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase));
}
