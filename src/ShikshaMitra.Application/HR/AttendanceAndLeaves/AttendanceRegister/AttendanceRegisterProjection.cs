using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.Employees;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterLockState;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceRegisterProjection
{
	public static List<RegisterProjection> ApplyPayrollStateFilter(
		List<RegisterProjection> rows,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		AttendanceRegisterPayrollState state)
	{
		return state switch
		{
			AttendanceRegisterPayrollState.Editable => rows.Where(x => x.IsMarked && !x.IsHoliday && !IsPayrollLocked(x, lockLookup)).ToList(),
			AttendanceRegisterPayrollState.Locked => rows.Where(x => IsPayrollLocked(x, lockLookup)).ToList(),
			_ => rows
		};
	}

	public static AttendanceRegisterSummaryDto BuildSummary(
		List<RegisterProjection> rows,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		HashSet<DateOnly> dayLocks,
		int staffCount)
	{
		var summary = new AttendanceRegisterSummaryDto { StaffCount = staffCount };
		foreach (var row in rows)
		{
			switch (row.Status)
			{
				case AttendanceRegisterStatus.Present: summary.Present++; break;
				case AttendanceRegisterStatus.HalfDay: summary.HalfDay++; break;
				case AttendanceRegisterStatus.Absent: summary.Absent++; break;
				case AttendanceRegisterStatus.NotMarked: summary.NotMarked++; break;
			}
			if (IsLocked(row, lockLookup, dayLocks)) summary.Locked++;
			else summary.Editable++;
			if (IsMissingPunch(row)) summary.MissingPunch++;
			if (row.IsHoliday) summary.Holiday++;
		}
		return summary;
	}

	public static AttendanceRegisterSummaryDto SummarizeGroups(List<GroupedRegisterProjection> groups)
	{
		var summary = new AttendanceRegisterSummaryDto { StaffCount = groups.Count };
		foreach (var group in groups)
		{
			summary.Present += group.Present;
			summary.HalfDay += group.HalfDay;
			summary.Absent += group.Absent;
			summary.NotMarked += group.RecordedDays - group.Present - group.HalfDay - group.Absent;
			summary.Editable += group.Editable;
			summary.Locked += group.Locked;
			summary.MissingPunch += group.MissingPunch;
			summary.Holiday += group.Holiday;
		}
		return summary;
	}

	public static GroupedRegisterProjection BuildGroupedEmployee(
		List<RegisterProjection> rows,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		HashSet<DateOnly> dayLocks)
	{
		var orderedRows = rows.OrderBy(x => x.AttendanceDate).ToList();
		var first = orderedRows[0];
		var summary = BuildSummary(orderedRows, lockLookup, dayLocks, 1);
		return new GroupedRegisterProjection
		{
			EmployeeId = first.EmployeeId,
			EmployeeName = first.EmployeeName,
			EmployeeCode = first.EmployeeCode,
			Department = first.Department,
			EmployeeGroup = first.EmployeeGroup,
			EducationMediumStageId = first.EducationMediumStageId,
			RoleId = first.RoleId,
			RecordedDays = orderedRows.Count,
			Present = summary.Present,
			HalfDay = summary.HalfDay,
			Absent = summary.Absent,
			Editable = summary.Editable,
			Locked = summary.Locked,
			MissingPunch = summary.MissingPunch,
			Holiday = summary.Holiday,
			HasExceptions = summary.Absent > 0 || summary.HalfDay > 0 || summary.MissingPunch > 0 || summary.Locked > 0,
			RawDays = orderedRows
		};
	}

	public static bool MatchesExceptionFilter(GroupedRegisterProjection group, AttendanceRegisterExceptionFilter filter) =>
		filter switch
		{
			AttendanceRegisterExceptionFilter.NeedsAttention => group.HasExceptions,
			AttendanceRegisterExceptionFilter.Absent => group.Absent > 0,
			AttendanceRegisterExceptionFilter.HalfDay => group.HalfDay > 0,
			AttendanceRegisterExceptionFilter.MissingPunch => group.MissingPunch > 0,
			AttendanceRegisterExceptionFilter.Locked => group.Locked > 0,
			_ => true
		};

	public static string ToMonthYear(DateOnly date) => $"{date.Year:D4}-{date.Month:D2}";

	public static DateOnly ParseMonthDate(string monthYear) => new(int.Parse(monthYear[..4]), int.Parse(monthYear[5..7]), 1);

	public static AttendanceRegisterStatus MapRegisterStatus(EmployeeAttendanceStatusType status) =>
		status switch
		{
			EmployeeAttendanceStatusType.Present => AttendanceRegisterStatus.Present,
			EmployeeAttendanceStatusType.HalfDayLeave => AttendanceRegisterStatus.HalfDay,
			EmployeeAttendanceStatusType.FullDayLeave => AttendanceRegisterStatus.Absent,
			_ => AttendanceRegisterStatus.NotMarked
		};

	public static bool IsEmployeeEligibleOnDate(EmployeeProjection employee, DateOnly date)
	{
		var startDate = employee.DateOfJoining ?? DateOnly.FromDateTime(employee.CreationTime.Date);
		if (startDate > date)
		{
			return false;
		}

		return EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(
			employee.EmployeeStatusType,
			employee.StatusEffectiveDate,
			date);
	}

	public static bool MatchesSearch(RegisterProjection row, string search) =>
		Contains(row.EmployeeName, search) ||
		Contains(row.EmployeeCode, search) ||
		Contains(row.MobileNo, search) ||
		Contains(row.Remarks, search);

	public static int NormalizeMaxResultCount(int maxResultCount) => maxResultCount is <= 0 or > 500 ? 50 : maxResultCount;

	private static bool Contains(string? value, string search) =>
		!string.IsNullOrWhiteSpace(value) &&
		value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
