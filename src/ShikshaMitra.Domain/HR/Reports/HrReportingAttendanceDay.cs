using System;

namespace ShikshaMitra.HR.Reports;

public static class HrReportingAttendanceDay
{
	public static string State(DateOnly date, DateTimeOffset capturedAt, string timezone, TimeOnly? shiftStart, TimeOnly? shiftEnd)
	{
		var local = TimeZoneInfo.ConvertTime(capturedAt, TimeZoneInfo.FindSystemTimeZoneById(timezone)).DateTime;
		var today = DateOnly.FromDateTime(local);
		if (date > today) return "Future";
		if (date == today) return "Open";
		if (date == today.AddDays(-1) && shiftStart.HasValue && shiftEnd.HasValue && shiftEnd <= shiftStart && TimeOnly.FromDateTime(local) <= shiftEnd) return "Open";
		return "Closed";
	}
}