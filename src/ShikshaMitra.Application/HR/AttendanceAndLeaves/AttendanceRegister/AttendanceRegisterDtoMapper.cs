using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using ShikshaMitra.HR.Policies;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceRegisterDtoMapper
{
	public static GroupedAttendanceRegisterEmployeeDto MapGroupedEmployee(
		GroupedRegisterProjection group,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		Dictionary<Guid, string> stageNames,
		HashSet<DateOnly> dayLocks) =>
		new()
		{
			EmployeeId = group.EmployeeId,
			EmployeeName = group.EmployeeName,
			EmployeeCode = group.EmployeeCode,
			Department = group.Department,
			EmployeeGroup = group.EmployeeGroup?.ToString(),
			EducationMediumStageId = group.EducationMediumStageId,
			EducationMediumStageName = group.EducationMediumStageId.HasValue
				? stageNames.GetValueOrDefault(group.EducationMediumStageId.Value)
				: null,
			RoleId = group.RoleId,
			RecordedDays = group.RecordedDays,
			Present = group.Present,
			HalfDay = group.HalfDay,
			Absent = group.Absent,
			Editable = group.Editable,
			Locked = group.Locked,
			MissingPunch = group.MissingPunch,
			Holiday = group.Holiday,
			HasExceptions = group.HasExceptions,
			Days = group.RawDays.Select(x => MapRow(x, lockLookup, stageNames, dayLocks)).ToList()
		};

	public static AttendanceRegisterRowDto MapRow(
		RegisterProjection row,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		Dictionary<Guid, string> stageNames,
		HashSet<DateOnly> dayLocks)
	{
		var payrollMonth = AttendanceRegisterProjection.ToMonthYear(row.AttendanceDate);
		var lockInfo = lockLookup.GetValueOrDefault((row.EmployeeId, payrollMonth), new PayrollLockInfo(false, null));
		var isDayLocked = dayLocks.Contains(row.AttendanceDate);
		var holidayReason = row.IsHoliday ? $"Holiday: {row.HolidayName}" : null;
		var lockReason = holidayReason ?? (isDayLocked ? "Attendance day is locked." : lockInfo.Reason);
		return new AttendanceRegisterRowDto
		{
			AttendanceId = row.AttendanceId,
			EmployeeId = row.EmployeeId,
			EmployeeName = row.EmployeeName,
			EmployeeCode = row.EmployeeCode,
			Department = row.Department,
			EmployeeGroup = row.EmployeeGroup?.ToString(),
			EducationMediumStageId = row.EducationMediumStageId,
			EducationMediumStageName = row.EducationMediumStageId.HasValue
				? stageNames.GetValueOrDefault(row.EducationMediumStageId.Value)
				: null,
			RoleId = row.RoleId,
			AttendanceDate = row.AttendanceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			PayrollMonth = payrollMonth,
			InTime = row.TimeIn?.ToString("o"),
			OutTime = row.TimeOut?.ToString("o"),
			HoursWorked = CalculateHoursWorked(row.TimeIn, row.TimeOut),
			Remarks = row.Remarks,
			CaptureMode = HrPolicyMapping.MapCaptureMode(row.CaptureMode),
			Source = HrPolicyMapping.MapAttendanceSource(row.Source),
			Status = row.Status,
			IsMarked = row.IsMarked,
			IsDayLocked = isDayLocked,
			IsPayrollLocked = lockInfo.IsLocked,
			IsHoliday = row.IsHoliday,
			HolidayName = row.HolidayName,
			HolidayReason = holidayReason,
			CanEdit = !lockInfo.IsLocked && !isDayLocked && !row.IsHoliday,
			LockReason = lockReason
		};
	}

	private static string? CalculateHoursWorked(DateTimeOffset? timeIn, DateTimeOffset? timeOut)
	{
		if (!timeIn.HasValue || !timeOut.HasValue)
		{
			return null;
		}

		var duration = timeOut.Value - timeIn.Value;
		var hours = (int)duration.TotalHours;
		return $"{hours:D2}:{duration.Minutes:D2}";
	}
}
