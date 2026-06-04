using System;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public static class SalarySlipAdjustmentComponentMapper
{
	public static EmployeePayrollComponent ToPayrollComponent(PayrollAdjustment adjustment) =>
		new(
			adjustment.Id,
			adjustment.EmployeeId,
			adjustment.Name,
			componentId: null,
			adjustment.Description ?? adjustment.Reason,
			adjustment.Amount,
			isFixed: true,
			isTemp: true,
			applicableMonth: DateTime.Parse($"{adjustment.MonthYear}-01"),
			SalaryComponentType.Fixed,
			SalaryComponentDependency.None,
			adjustment.IsEarning,
			usesMasterValue: false);
}
