using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Common;
using ShikshaMitra.TenantSettings;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class TakeAttendanceDayProjection
{
	public static List<TakeAttendanceEmployeeRowDto> BuildRows(
		IReadOnlyList<Employee> employees,
		IReadOnlyDictionary<Guid, EmployeeAttendance> attendanceByEmployee,
		IReadOnlyDictionary<Guid, ApprovedAttendanceLeaveRow> approvedLeaveByEmployee,
		IReadOnlyDictionary<Guid, ShiftManagement> shifts,
		IReadOnlyDictionary<Guid, AttendanceCaptureMode> captureModeByEmployee,
		DateTime date,
		Holiday? holiday,
		string? holidayReason)
	{
		var items = new List<TakeAttendanceEmployeeRowDto>();
		foreach (var employee in employees)
		{
			attendanceByEmployee.TryGetValue(employee.Id, out var attendance);
			approvedLeaveByEmployee.TryGetValue(employee.Id, out var approvedLeave);
			shifts.TryGetValue(employee.ShiftId ?? Guid.Empty, out var shift);
			var captureMode = captureModeByEmployee.GetValueOrDefault(employee.Id, AttendanceCaptureMode.Integrated);
			var warnings = new List<string>();
			if (attendance != null && approvedLeave != null)
			{
				warnings.Add("Approved leave exists for this date but attendance was manually overridden.");
			}

			items.Add(new TakeAttendanceEmployeeRowDto
			{
				EmployeeId = employee.Id,
				IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
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
				CaptureMode = HrPolicyMapping.MapCaptureMode(captureMode),
				Source = attendance == null && approvedLeave != null
					? "approved_leave"
					: HrPolicyMapping.MapAttendanceSource(attendance?.Source ?? AttendanceRecordSource.Manual),
				Warnings = warnings,
				Errors = [],
				IsHoliday = holiday != null,
				HolidayName = holiday?.HolidayName,
				HolidayReason = holidayReason,
				IsApprovedLeave = approvedLeave != null,
				ApprovedLeaveId = approvedLeave?.LeaveId,
				ApprovedLeaveKind = approvedLeave == null ? null : approvedLeave.HalfDay ? "HalfDay" : "FullDay",
				ApprovedLeaveLabel = approvedLeave == null ? null : approvedLeave.HalfDay ? "On Leave (Half Day)" : "On Leave",
				Status = attendance == null
					? approvedLeave == null ? TakeAttendanceStatus.NotMarked : TakeAttendanceStatus.OnLeave
					: MapDomainStatus(attendance.StatusType)
			});
		}

		return items;
	}

	public static TakeAttendanceSummaryDto BuildSummary(
		IReadOnlyList<EmployeeAttendance> attendanceRows,
		IReadOnlyDictionary<Guid, ApprovedAttendanceLeaveRow> approvedLeaveByEmployee,
		int scopedEmployeeCount)
	{
		var onLeaveCount = CountApprovedLeaveWithoutAttendance(attendanceRows, approvedLeaveByEmployee);

		return new TakeAttendanceSummaryDto
		{
			Present = attendanceRows.Count(x => x.StatusType == EmployeeAttendanceStatusType.Present),
			HalfDay = attendanceRows.Count(x => x.StatusType == EmployeeAttendanceStatusType.HalfDayLeave),
			Absent = attendanceRows.Count(x => x.StatusType == EmployeeAttendanceStatusType.FullDayLeave),
			OnLeave = onLeaveCount,
			NotMarked = scopedEmployeeCount - attendanceRows.Count - onLeaveCount
		};
	}

	private static TakeAttendanceStatus MapDomainStatus(EmployeeAttendanceStatusType status) =>
		status switch
		{
			EmployeeAttendanceStatusType.Present => TakeAttendanceStatus.Present,
			EmployeeAttendanceStatusType.HalfDayLeave => TakeAttendanceStatus.HalfDay,
			EmployeeAttendanceStatusType.FullDayLeave => TakeAttendanceStatus.Absent,
			_ => TakeAttendanceStatus.NotMarked
		};

	private static int CountApprovedLeaveWithoutAttendance(
		IReadOnlyList<EmployeeAttendance> attendanceRows,
		IReadOnlyDictionary<Guid, ApprovedAttendanceLeaveRow> approvedLeaveByEmployee)
	{
		var attendanceEmployeeIds = attendanceRows.Select(x => x.EmployeeId).ToHashSet();
		return approvedLeaveByEmployee.Keys.Count(employeeId => !attendanceEmployeeIds.Contains(employeeId));
	}

	private static string FormatName(string? first, string? middle, string? last)
	{
		var parts = new[] { first, middle, last }.Where(p => !string.IsNullOrWhiteSpace(p));
		return string.Join(" ", parts);
	}
}
