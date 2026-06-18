using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public static class AttendancePolicyLateArrivalPenaltyRules
{
	public static int GetEffectiveTrigger(AttendancePolicy policy) =>
		Math.Max(
			policy.MaxLateAllowedPerMonth + 1,
			policy.PenaltyCountThreshold > 0 ? policy.PenaltyCountThreshold : 1);

	public static bool IsLateArrival(DateTimeOffset timeIn, ShiftManagement shift, int graceTime) =>
		(timeIn.TimeOfDay - shift.StartTime.ToTimeSpan()).TotalMinutes > graceTime;

	public static int CountSalaryDeductionOccurrences(
		IReadOnlyList<EmployeeAttendance> attendanceRecords,
		AttendancePolicy? policy,
		ShiftManagement? shift,
		int year,
		int month,
		DateOnly? employmentStartDate = null)
	{
		if (policy?.PenaltyType != AttendancePenaltyType.SalaryDeduction || shift == null)
		{
			return 0;
		}

		var monthStart = new DateOnly(year, month, 1);
		var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
		var startDate = employmentStartDate.HasValue && employmentStartDate.Value > monthStart
			? employmentStartDate.Value
			: monthStart;
		var effectiveTrigger = GetEffectiveTrigger(policy);
		var monthlyLateCount = 0;
		var penaltyOccurrences = 0;

		foreach (var attendance in attendanceRecords
			.Where(x => x.AttendanceDate >= startDate &&
			            x.AttendanceDate <= monthEnd &&
			            x.TimeIn.HasValue)
			.GroupBy(x => x.AttendanceDate)
			.Select(x => x.Last())
			.OrderBy(x => x.AttendanceDate))
		{
			if (!IsLateArrival(attendance.TimeIn!.Value, shift, policy.GraceTime))
			{
				continue;
			}

			monthlyLateCount++;
			if (monthlyLateCount >= effectiveTrigger)
			{
				penaltyOccurrences++;
			}
		}

		return penaltyOccurrences;
	}
}
