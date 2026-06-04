using System;
using System.Globalization;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public static class AttendanceIntegrationRunParsing
{
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

	public static DateOnly ParseRowDate(string attendanceDate)
	{
		if (DateTime.TryParseExact(attendanceDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
		{
			return DateOnly.FromDateTime(dt);
		}

		if (DateTime.TryParse(attendanceDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
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
}
