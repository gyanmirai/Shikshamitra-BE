using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;

namespace ShikshaMitra.HR.Payroll.Services;

public static class LateArrivalSalaryDeductionComponentBuilder
{
	public const string ComponentName = "Late Arrival Salary Deduction";

	public static EmployeePayrollComponent? Build(
		Guid employeeId,
		IReadOnlyList<EmployeeAttendance> attendanceRecords,
		IReadOnlyList<EmployeePayrollComponent> salaryStructureComponents,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		ShiftManagement? shift,
		AttendancePolicy? attendancePolicy,
		DateOnly? employmentStartDate = null)
	{
		if (basis.BasisDays <= 0m)
		{
			return null;
		}

		var penaltyOccurrences = AttendancePolicyLateArrivalPenaltyRules.CountSalaryDeductionOccurrences(
			attendanceRecords,
			attendancePolicy,
			shift,
			year,
			month,
			employmentStartDate);
		if (penaltyOccurrences <= 0)
		{
			return null;
		}

		var fullGrossSalary = salaryStructureComponents
			.Where(component => component.IsEarning)
			.Sum(component => component.Amount);
		if (fullGrossSalary <= 0m)
		{
			return null;
		}

		var amount = Math.Round(
			fullGrossSalary / basis.BasisDays * 0.5m * penaltyOccurrences,
			2,
			MidpointRounding.AwayFromZero);
		if (amount <= 0m)
		{
			return null;
		}

		return new EmployeePayrollComponent(
			Guid.NewGuid(),
			employeeId,
			ComponentName,
			componentId: null,
			description: $"{penaltyOccurrences} qualifying late arrival(s)",
			amount,
			isFixed: true,
			isTemp: true,
			applicableMonth: new DateTime(year, month, 1),
			SalaryComponentType.Fixed,
			SalaryComponentDependency.None,
			isEarning: false,
			usesMasterValue: false);
	}
}
