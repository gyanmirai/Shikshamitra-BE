using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollComponentMonthScope
{
	public static List<EmployeePayrollComponent> FilterForMonth(
		IEnumerable<EmployeePayrollComponent> components,
		int year,
		int month)
	{
		return components
			.Where(component => AppliesToMonth(component, year, month))
			.ToList();
	}

	public static bool AppliesToMonth(EmployeePayrollComponent component, int year, int month)
	{
		if (!component.IsTemp)
		{
			return true;
		}

		return component.ApplicableMonth.HasValue &&
			component.ApplicableMonth.Value.Year == year &&
			component.ApplicableMonth.Value.Month == month;
	}
}
