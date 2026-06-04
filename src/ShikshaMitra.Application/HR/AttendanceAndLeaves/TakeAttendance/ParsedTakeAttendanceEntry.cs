using ShikshaMitra.Common;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

public sealed record ParsedTakeAttendanceEntry(
	TakeAttendanceSaveEntryDto Entry,
	EmployeeAttendanceStatusType Status);
