using System;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public static class PayslipCorrectionAttendanceRules
{
	public static void EnsureValidPunches(EmployeeAttendance attendance)
	{
		// Attendance clock values are encoded at offset zero. Compare the same clock
		// used by payroll, including when a database reader supplies another offset.
		EnsureDate(attendance.TimeIn, "timeIn", "Time in");
		EnsureDate(attendance.TimeOut, "timeOut", "Time out");
		if (!attendance.TimeIn.HasValue || !attendance.TimeOut.HasValue) return;
		// Preserve the supported explicit absent-day midnight representation.
		if (attendance.StatusType == EmployeeAttendanceStatusType.FullDayLeave &&
			attendance.TimeIn.Value.UtcDateTime.TimeOfDay == TimeSpan.Zero &&
			attendance.TimeOut.Value.UtcDateTime.TimeOfDay == TimeSpan.Zero) return;
		if (attendance.TimeOut.Value <= attendance.TimeIn.Value)
			throw Invalid("timeOut", "Time out must be after time in.");

		void EnsureDate(DateTimeOffset? value, string field, string label)
		{
			if (value.HasValue && DateOnly.FromDateTime(value.Value.UtcDateTime) != attendance.AttendanceDate)
				throw Invalid(field, $"{label} must belong to the attendance date.");
		}

		BusinessException Invalid(string field, string message) =>
			new BusinessException(HRErrorCodes.AttendancePolicyEvaluationInvalidPunches,
				$"{attendance.AttendanceDate:yyyy-MM-dd}: {message}")
				.WithData("attendanceDate", attendance.AttendanceDate.ToString("yyyy-MM-dd"))
				.WithData("field", field);
	}
}
