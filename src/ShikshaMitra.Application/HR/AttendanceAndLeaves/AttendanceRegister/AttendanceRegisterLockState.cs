using System;
using System.Collections.Generic;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceRegisterLockState
{
	public static bool IsPayrollLocked(
		RegisterProjection row,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup) =>
		lockLookup.GetValueOrDefault((row.EmployeeId, AttendanceRegisterProjection.ToMonthYear(row.AttendanceDate)), new PayrollLockInfo(false, null)).IsLocked;

	public static bool IsLocked(
		RegisterProjection row,
		Dictionary<(Guid EmployeeId, string MonthYear), PayrollLockInfo> lockLookup,
		HashSet<DateOnly> dayLocks) =>
		row.IsHoliday || dayLocks.Contains(row.AttendanceDate) || IsPayrollLocked(row, lockLookup);

	public static bool IsMissingPunch(RegisterProjection row) =>
		row.Status == AttendanceRegisterStatus.Present &&
		row.CaptureMode != AttendanceCaptureMode.ManualStatus &&
		(!row.TimeIn.HasValue || !row.TimeOut.HasValue);
}
