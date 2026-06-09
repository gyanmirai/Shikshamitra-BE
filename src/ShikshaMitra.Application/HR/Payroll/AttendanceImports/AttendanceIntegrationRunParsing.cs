using System;
using System.Globalization;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public static class AttendanceIntegrationRunParsing
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
	private static readonly string[] SupportedTimeFormats =
	[
		"HH:mm",
		"H:mm",
		"hh:mm tt",
		"h:mm tt",
		"hh:mmtt",
		"h:mmtt",
		"HH:mm:ss",
		"hh:mm:ss tt",
		"h:mm:ss tt"
	];

	public static string? Truncate(string? value, int max)
	{
		if (string.IsNullOrEmpty(value) || value.Length <= max)
		{
			return value;
		}

		return value[..max];
	}

	public static DateTimeOffset? ParseNullableOffset(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
		{
			return parsed;
		}

		throw new BusinessException(
			HRErrorCodes.AttendancePolicyEvaluationInvalidPunches,
			"Attendance time is invalid.");
	}

	public static DateTimeOffset? NormalizeNullableOffset(string? value, DateOnly attendanceDate)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var trimmed = value.Trim();
		if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedOffset))
		{
			var dateTime = attendanceDate.ToDateTime(TimeOnly.FromDateTime(parsedOffset.DateTime), DateTimeKind.Unspecified);
			return new DateTimeOffset(dateTime, parsedOffset.Offset);
		}

		if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime))
		{
			return new DateTimeOffset(attendanceDate.ToDateTime(TimeOnly.FromDateTime(parsedDateTime)), TimeSpan.Zero);
		}

		if (TryParseTime(trimmed, out var parsedTime))
		{
			var localDateTime = attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(parsedTime), DateTimeKind.Unspecified);
			return new DateTimeOffset(localDateTime, DefaultImportTimeZone.GetUtcOffset(localDateTime));
		}

		throw new BusinessException(
			HRErrorCodes.AttendancePolicyEvaluationInvalidPunches,
			"Attendance time is invalid.");
	}

	public static DateOnly ParseRowDate(string attendanceDate)
	{
		if (DateTime.TryParseExact(attendanceDate, SupportedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
		{
			return DateOnly.FromDateTime(dt);
		}

		throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);
	}

	public static string? ToApiMonthYear(string? internalMonthYear)
	{
		if (string.IsNullOrWhiteSpace(internalMonthYear))
		{
			return null;
		}

		if (!DateTime.TryParseExact(
			    $"{internalMonthYear}-01",
			    "yyyy-MM-dd",
			    CultureInfo.InvariantCulture,
			    DateTimeStyles.None,
			    out var monthDate))
		{
			return null;
		}

		return $"{monthDate.ToString("MMMM", CultureInfo.InvariantCulture).ToUpperInvariant()}-{monthDate.Year}";
	}

	private static bool TryParseTime(string value, out TimeSpan time)
	{
		if (DateTime.TryParseExact(
			    value,
			    SupportedTimeFormats,
			    CultureInfo.InvariantCulture,
			    DateTimeStyles.AllowWhiteSpaces,
			    out var parsedDateTime))
		{
			time = parsedDateTime.TimeOfDay;
			return true;
		}

		return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time);
	}
}
