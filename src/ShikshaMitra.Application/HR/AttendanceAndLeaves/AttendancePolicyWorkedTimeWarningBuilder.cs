using System;
using System.Collections.Generic;
using System.Globalization;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public static class AttendancePolicyWorkedTimeWarningBuilder
{
	public static void AddEarlyExitWarning(
		AttendancePolicy policy,
		ShiftManagement shift,
		DateTimeOffset timeOut,
		List<string> warnings)
	{
		if (!policy.EarlyExitEnabled)
		{
			return;
		}

		var earlyExitMinutes = (shift.EndTime.ToTimeSpan() - timeOut.TimeOfDay).TotalMinutes;
		if (earlyExitMinutes <= policy.EarlyExitGraceTime)
		{
			return;
		}

		warnings.Add(string.Format(
			CultureInfo.InvariantCulture,
			"Early exit warning: left {0:0.##} minutes before shift end, exceeding grace time {1} minutes.",
			earlyExitMinutes,
			policy.EarlyExitGraceTime));
	}

	public static string BuildWorkedTimeWarning(
		EmployeeAttendanceStatusType evaluatedStatus,
		double workedMinutes,
		double shiftMinutes,
		double workedPercentage,
		double fullDayThresholdPercentage)
	{
		var label = evaluatedStatus switch
		{
			EmployeeAttendanceStatusType.HalfDayLeave => "Half Day",
			EmployeeAttendanceStatusType.FullDayLeave => "Absent",
			_ => "Present"
		};

		return string.Format(
			CultureInfo.InvariantCulture,
			"Auto-marked {0}: worked {1:0.##}h of {2:0.##}h shift ({3:0.##}%), below full-day threshold {4:0.##}%.",
			label,
			workedMinutes / 60d,
			shiftMinutes / 60d,
			workedPercentage,
			fullDayThresholdPercentage);
	}

	public static string BuildLateArrivalPenaltyWarning(
		EmployeeAttendanceStatusType evaluatedStatus,
		double lateMinutes,
		int graceTime,
		int monthlyLateCount,
		int effectiveTrigger)
	{
		var label = evaluatedStatus switch
		{
			EmployeeAttendanceStatusType.HalfDayLeave => "Half Day",
			EmployeeAttendanceStatusType.FullDayLeave => "Absent",
			_ => "Present"
		};

		return string.Format(
			CultureInfo.InvariantCulture,
			"Auto-marked {0}: late arrival {1:0.##} minutes exceeded grace time {2} minutes, monthly late count {3} reached trigger {4}.",
			label,
			lateMinutes,
			graceTime,
			monthlyLateCount,
			effectiveTrigger);
	}

	public static string BuildLateArrivalBelowTriggerWarning(
		double lateMinutes,
		int graceTime,
		int monthlyLateCount,
		int effectiveTrigger) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Late arrival warning: late arrival {0:0.##} minutes exceeded grace time {1} minutes; monthly late count {2} has not reached trigger {3}.",
			lateMinutes,
			graceTime,
			monthlyLateCount,
			effectiveTrigger);

	public static string BuildLateArrivalWarningActionWarning(
		double lateMinutes,
		int graceTime,
		int monthlyLateCount,
		int effectiveTrigger) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Late arrival warning action: late arrival {0:0.##} minutes exceeded grace time {1} minutes; monthly late count {2} reached trigger {3}.",
			lateMinutes,
			graceTime,
			monthlyLateCount,
			effectiveTrigger);

	public static string BuildLateArrivalSalaryDeductionWarning(
		double lateMinutes,
		int graceTime,
		int monthlyLateCount,
		int effectiveTrigger) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Late arrival salary deduction marker: late arrival {0:0.##} minutes exceeded grace time {1} minutes; monthly late count {2} reached trigger {3}.",
			lateMinutes,
			graceTime,
			monthlyLateCount,
			effectiveTrigger);

	public static string BuildLateArrivalNoPenaltyWarning(
		double lateMinutes,
		int graceTime,
		int monthlyLateCount) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Late arrival warning: late arrival {0:0.##} minutes exceeded grace time {1} minutes; monthly late count is {2}, with no late penalty configured.",
			lateMinutes,
			graceTime,
			monthlyLateCount);

	public static string BuildLateArrivalAlreadyDowngradedWarning(
		double lateMinutes,
		int graceTime,
		int monthlyLateCount,
		int effectiveTrigger,
		EmployeeAttendanceStatusType evaluatedStatus) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Late arrival warning: late arrival {0:0.##} minutes exceeded grace time {1} minutes and monthly late count {2} reached trigger {3}, but status stayed {4} because it was already equal or more severe.",
			lateMinutes,
			graceTime,
			monthlyLateCount,
			effectiveTrigger,
			evaluatedStatus);
}
