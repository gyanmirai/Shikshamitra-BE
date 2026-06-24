using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal static class PayrollLateArrivalDeductionDiagnostics
{
	public static string? BuildConfigurationWarning(
		Employee employee,
		IReadOnlyCollection<EmployeeAttendance> attendanceRecords,
		int year,
		int month,
		AttendancePolicy policy,
		ShiftManagement? shift)
	{
		if (policy.PenaltyType != AttendancePenaltyType.SalaryDeduction)
		{
			return null;
		}

		if (!policy.PenaltyDeductionAmount.HasValue || policy.PenaltyDeductionAmount.Value <= 0m)
		{
			return "Late arrival salary deduction is enabled, but the attendance policy has no positive deduction amount.";
		}

		if (shift == null)
		{
			return "Late arrival salary deduction is enabled, but the employee has no shift assigned.";
		}

		var monthAttendance = attendanceRecords
			.Where(x => x.AttendanceDate.Year == year && x.AttendanceDate.Month == month)
			.ToList();
		if (monthAttendance.Count > 0 && monthAttendance.All(x => !x.TimeIn.HasValue))
		{
			return "Late arrival salary deduction is enabled, but this month's attendance has no in-time values.";
		}

		return null;
	}
}
