using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal static class PayrollIssueCodeBuilder
{
	public static List<string> Build(
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		decimal basisDays,
		decimal payableDays,
		decimal netPay,
		decimal? previousMonthNetPay,
		decimal? netPayDeltaPercent,
		bool hasAttendanceOrPaidLeave)
	{
		var issues = new List<string>();

		if (payrollDetails == null || components.Count == 0)
		{
			issues.Add(IssueCodes.MissingSalaryStructure);
		}

		if (basisDays <= 0)
		{
			issues.Add(IssueCodes.NoWorkingDays);
		}

		if (basisDays > 0 && !hasAttendanceOrPaidLeave)
		{
			issues.Add(IssueCodes.NoAttendance);
		}
		else if (basisDays > 0 && payableDays < basisDays)
		{
			issues.Add(IssueCodes.LowAttendance);
		}

		if (netPay < 0)
		{
			issues.Add(IssueCodes.NegativeNetPay);
		}

		if (!previousMonthNetPay.HasValue)
		{
			issues.Add(IssueCodes.NoPreviousMonthComparison);
		}
		else if (netPayDeltaPercent.HasValue && Math.Abs(netPayDeltaPercent.Value) >= 10m)
		{
			issues.Add(IssueCodes.LargeSalaryVariance);
		}

		return issues;
	}

	public static bool HasAttendanceOrPaidLeave(
		IReadOnlyList<EmployeeAttendance> attendanceRecords,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		DateOnly? employmentStartDate,
		int year,
		int month)
	{
		var monthStartDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var startDate = employmentStartDate.HasValue && employmentStartDate.Value > monthStartDate
			? employmentStartDate.Value
			: monthStartDate;

		if (attendanceRecords.Any(x => x.AttendanceDate >= startDate && x.AttendanceDate <= endDate))
		{
			return true;
		}

		return leaveUnitsByDate?.Any(x =>
			x.Key >= startDate &&
			x.Key <= endDate &&
			x.Value.PaidUnits > 0m) == true;
	}
}
