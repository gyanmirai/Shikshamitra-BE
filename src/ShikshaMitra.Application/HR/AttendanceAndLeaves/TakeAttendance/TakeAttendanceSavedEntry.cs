using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

public sealed record TakeAttendanceSavedEntry(
	Employee Employee,
	EmployeeAttendance Attendance);
