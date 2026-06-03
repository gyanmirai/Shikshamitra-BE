using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class TakeAttendanceDayProjection
{
	public static List<TakeAttendanceEmployeeRowDto> BuildRows(
		IReadOnlyList<Employee> employees,
		IReadOnlyDictionary<Guid, EmployeeAttendance> attendanceByEmployee,
		IReadOnlyDictionary<Guid, ShiftManagement> shifts,
		DateTime date,
		Holiday? holiday,
		string? holidayReason)
	{
		var items = new List<TakeAttendanceEmployeeRowDto>();
		foreach (var employee in employees)
		{
			attendanceByEmployee.TryGetValue(employee.Id, out var attendance);
			shifts.TryGetValue(employee.ShiftId ?? Guid.Empty, out var shift);
			items.Add(new TakeAttendanceEmployeeRowDto
			{
				EmployeeId = employee.Id,
				FullName = FormatName(employee.FirstName, employee.MiddleName, employee.LastName),
				EmployeeCode = employee.EmployeeCode ?? string.Empty,
				Department = employee.Department,
				DateIso = new DateTimeOffset(date.Date, TimeSpan.Zero).ToString("o"),
				InTime = attendance?.TimeIn?.ToString("o"),
				OutTime = attendance?.TimeOut?.ToString("o"),
				ShiftName = shift?.ShiftName,
				ShiftStartTime = shift?.StartTime.ToString("HH:mm:ss"),
				ShiftEndTime = shift?.EndTime.ToString("HH:mm:ss"),
				Remarks = attendance?.Remarks,
				Warnings = [],
				Errors = [],
				IsHoliday = holiday != null,
				HolidayName = holiday?.HolidayName,
				HolidayReason = holidayReason,
				Status = attendance == null
					? TakeAttendanceStatus.NotMarked
					: MapDomainStatus(attendance.StatusType)
			});
		}

		return items;
	}

	public static TakeAttendanceSummaryDto BuildSummary(
		IReadOnlyList<EmployeeAttendance> attendanceRows,
		int scopedEmployeeCount) =>
		new()
		{
			Present = attendanceRows.Count(x => x.StatusType == EmployeeAttendanceStatusType.Present),
			HalfDay = attendanceRows.Count(x => x.StatusType == EmployeeAttendanceStatusType.HalfDayLeave),
			Absent = attendanceRows.Count(x => x.StatusType == EmployeeAttendanceStatusType.FullDayLeave),
			NotMarked = scopedEmployeeCount - attendanceRows.Count
		};

	private static TakeAttendanceStatus MapDomainStatus(EmployeeAttendanceStatusType status) =>
		status switch
		{
			EmployeeAttendanceStatusType.Present => TakeAttendanceStatus.Present,
			EmployeeAttendanceStatusType.HalfDayLeave => TakeAttendanceStatus.HalfDay,
			EmployeeAttendanceStatusType.FullDayLeave => TakeAttendanceStatus.Absent,
			_ => TakeAttendanceStatus.NotMarked
		};

	private static string FormatName(string? first, string? middle, string? last)
	{
		var parts = new[] { first, middle, last }.Where(p => !string.IsNullOrWhiteSpace(p));
		return string.Join(" ", parts);
	}
}
