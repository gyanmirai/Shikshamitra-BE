using ShikshaMitra.HR.WorkShifts;
using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class TakeAttendanceDayProjection
{
	internal sealed record EmployeeRow(Guid Id, EmployeeGroupType? EmployeeGroup,
		string? FirstName, string? MiddleName, string? LastName, string? EmployeeCode, string? Department);

	internal sealed record AttendanceRow(Guid EmployeeId, EmployeeAttendanceStatusType StatusType,
		DateTimeOffset? TimeIn, DateTimeOffset? TimeOut, string? Remarks, AttendanceRecordSource Source);

	public static List<TakeAttendanceEmployeeRowDto> BuildRows(
		IReadOnlyList<EmployeeRow> employees,
		IReadOnlyDictionary<Guid, AttendanceRow> attendanceByEmployee,
		IReadOnlyDictionary<Guid, ApprovedAttendanceLeaveRow> approvedLeaveByEmployee,
		IReadOnlyDictionary<Guid, EmployeeWorkShift> shifts,
		IReadOnlyDictionary<Guid, AttendanceCaptureMode> captureModeByEmployee,
		DateTime date,
		Holiday? holiday,
		string? holidayReason)
	{
		var items = new List<TakeAttendanceEmployeeRowDto>(employees.Count);
		var dateIso = new DateTimeOffset(date.Date, TimeSpan.Zero).ToString("o");
		foreach (var employee in employees)
		{
			attendanceByEmployee.TryGetValue(employee.Id, out var attendance);
			approvedLeaveByEmployee.TryGetValue(employee.Id, out var approvedLeave);
			shifts.TryGetValue(employee.Id, out var shift);
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
				DateIso = dateIso,
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