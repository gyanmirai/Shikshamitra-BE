using System;
using System.Globalization;
using ShikshaMitra.HR;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceImportCellParser
{
	private static readonly TimeZoneInfo DefaultImportTimeZone = TimeZoneInfo.Local;
	private static readonly string[] SupportedDateFormats =
	[
		"dd-MM-yyyy",
		"d-M-yyyy",
		"yyyy-MM-dd",
		"dd/MM/yyyy",
		"d/M/yyyy"
	];

	public static bool TryParseDate(object value, out DateOnly date)
	{
		switch (value)
		{
			case DateTime dateTime:
				date = DateOnly.FromDateTime(dateTime);
				return true;
			case string stringValue when DateTime.TryParseExact(
				stringValue.Trim(),
				SupportedDateFormats,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var parsedExact):
				date = DateOnly.FromDateTime(parsedExact);
				return true;
			default:
				date = default;
				return false;
		}
	}

	public static bool TryParseTime(object? value, out TimeSpan time)
	{
		switch (value)
		{
			case null:
				time = default;
				return false;
			case TimeSpan timeSpan:
				time = timeSpan;
				return true;
			case DateTime dateTime:
				time = dateTime.TimeOfDay;
				return true;
			case string stringValue when string.IsNullOrWhiteSpace(stringValue):
				time = default;
				return false;
			case string stringValue:
				return TryParseTimeString(stringValue, out time);
			default:
				time = default;
				return false;
		}
	}

	public static bool TryParseStatus(string input, out EmployeeAttendanceStatusType status)
	{
		var normalized = input.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
		if (normalized.Equals("Present", StringComparison.OrdinalIgnoreCase))
		{
			status = EmployeeAttendanceStatusType.Present;
			return true;
		}

		if (normalized.Equals("HalfDayLeave", StringComparison.OrdinalIgnoreCase) ||
			normalized.Equals("HalfDay", StringComparison.OrdinalIgnoreCase))
		{
			status = EmployeeAttendanceStatusType.HalfDayLeave;
			return true;
		}

		if (normalized.Equals("FullDayLeave", StringComparison.OrdinalIgnoreCase) ||
			normalized.Equals("Absent", StringComparison.OrdinalIgnoreCase) ||
			normalized.Equals("FullDay", StringComparison.OrdinalIgnoreCase))
		{
			status = EmployeeAttendanceStatusType.FullDayLeave;
			return true;
		}

		status = default;
		return false;
	}

	public static DateTimeOffset CombineDateAndTime(DateOnly attendanceDate, TimeSpan time)
	{
		var localDateTime = attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(time), DateTimeKind.Unspecified);
		var offset = DefaultImportTimeZone.GetUtcOffset(localDateTime);
		return new DateTimeOffset(localDateTime, offset);
	}

	private static bool TryParseTimeString(string value, out TimeSpan time)
	{
		var normalized = value.Trim();
		if (DateTime.TryParseExact(
			normalized,
			["HH:mm", "H:mm", "hh:mm tt", "h:mm tt", "hh:mmtt", "h:mmtt", "HH:mm:ss", "hh:mm:ss tt", "h:mm:ss tt"],
			CultureInfo.InvariantCulture,
			DateTimeStyles.AllowWhiteSpaces,
			out var parsedDateTime))
		{
			time = parsedDateTime.TimeOfDay;
			return true;
		}

		if (TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out var parsedTimeSpan))
		{
			time = parsedTimeSpan;
			return true;
		}

		time = default;
		return false;
	}
}
