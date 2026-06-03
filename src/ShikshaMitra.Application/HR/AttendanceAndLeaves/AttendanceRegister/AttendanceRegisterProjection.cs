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
		return new AttendanceRegisterSummaryDto
		{
			StaffCount = staffCount,
			Present = rows.Count(x => x.Status == AttendanceRegisterStatus.Present),
			HalfDay = rows.Count(x => x.Status == AttendanceRegisterStatus.HalfDay),
			Absent = rows.Count(x => x.Status == AttendanceRegisterStatus.Absent),
			NotMarked = rows.Count(x => x.Status == AttendanceRegisterStatus.NotMarked),
			Locked = rows.Count(x => IsLocked(x, lockLookup, dayLocks)),
			Editable = rows.Count(x => x.IsMarked && !IsLocked(x, lockLookup, dayLocks)),
			MissingPunch = rows.Count(IsMissingPunch),
			Holiday = rows.Count(x => x.IsHoliday)
		};
	}

	public static GroupedRegisterProjection BuildGroupedEmployee(
		List<RegisterProjection> rows,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		HashSet<DateOnly> dayLocks)
	{
		var first = rows.OrderBy(x => x.AttendanceDate).First();
		var orderedRows = rows.OrderBy(x => x.AttendanceDate).ToList();
		var absent = orderedRows.Count(x => x.Status == AttendanceRegisterStatus.Absent);
		var halfDay = orderedRows.Count(x => x.Status == AttendanceRegisterStatus.HalfDay);
		var missingPunch = orderedRows.Count(IsMissingPunch);
		var locked = orderedRows.Count(x => IsLocked(x, lockLookup, dayLocks));
		var holiday = orderedRows.Count(x => x.IsHoliday);
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
			Present = orderedRows.Count(x => x.Status == AttendanceRegisterStatus.Present),
			HalfDay = halfDay,
			Absent = absent,
			Editable = orderedRows.Count(x => x.IsMarked && !IsLocked(x, lockLookup, dayLocks)),
			Locked = locked,
			MissingPunch = missingPunch,
			Holiday = holiday,
			HasExceptions = absent > 0 || halfDay > 0 || missingPunch > 0 || locked > 0,
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
