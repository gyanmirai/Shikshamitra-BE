using System;
using System.Globalization;
using ShikshaMitra.HR.AttendanceAndLeaves;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

internal static class AttendanceImportSaveEntryParsing
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

	public static EmployeeAttendanceStatusType ParseDomainStatus(string statusWire) =>
		NormalizeStatusKey(statusWire) switch
		{
			"PRESENT" => EmployeeAttendanceStatusType.Present,
			"ABSENT" or "FULLDAYLEAVE" => EmployeeAttendanceStatusType.FullDayLeave,
			"HALFDAY" or "HALFDAYLEAVE" => EmployeeAttendanceStatusType.HalfDayLeave,
			_ => throw new BusinessException(HRErrorCodes.TakeAttendanceInvalidStatusWire)
		};

	public static string ToSaveWire(EmployeeAttendanceStatusType status) =>
		status switch
		{
			EmployeeAttendanceStatusType.Present => "Present",
			EmployeeAttendanceStatusType.HalfDayLeave => "HalfDay",
			_ => "Absent"
		};

	public static DateTimeOffset? ParseNullableOffset(string? value) =>
		string.IsNullOrWhiteSpace(value)
			? null
			: DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

	public static string NormalizeStatusKey(string? value) =>
		(value ?? string.Empty)
			.Trim()
			.Replace("_", string.Empty)
			.Replace("-", string.Empty)
			.Replace(" ", string.Empty)
			.ToUpperInvariant();

	public static DateOnly ParseImportRowDate(string? value)
	{
		if (DateTime.TryParseExact(
			    (value ?? string.Empty).Trim(),
			    SupportedDateFormats,
			    CultureInfo.InvariantCulture,
			    DateTimeStyles.None,
			    out var exact))
		{
			return DateOnly.FromDateTime(exact);
		}

		throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
			.WithData("reason", "Attendance date is invalid.");
	}

	public static NormalizedAttendanceImportTime NormalizeTimeForSave(string? value, DateOnly attendanceDate)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new NormalizedAttendanceImportTime(null, null);
		}

		var trimmed = value.Trim();
		if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedOffset))
		{
			var dateTime = attendanceDate.ToDateTime(TimeOnly.FromDateTime(parsedOffset.DateTime), DateTimeKind.Unspecified);
			var normalizedOffset = new DateTimeOffset(dateTime, parsedOffset.Offset);
			return new NormalizedAttendanceImportTime(
				normalizedOffset.ToString("o", CultureInfo.InvariantCulture),
				normalizedOffset.ToString("HH:mm", CultureInfo.InvariantCulture));
		}

		if (TryParseTime(trimmed, out var parsedTime))
		{
			var localDateTime = attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(parsedTime), DateTimeKind.Unspecified);
			var offset = DefaultImportTimeZone.GetUtcOffset(localDateTime);
			var dateTimeOffset = new DateTimeOffset(localDateTime, offset);
			return new NormalizedAttendanceImportTime(
				dateTimeOffset.ToString("o", CultureInfo.InvariantCulture),
				parsedTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
		}

		throw new BusinessException(HRErrorCodes.AttendanceImportRowMismatch)
			.WithData("reason", "Attendance time is invalid.");
	}

	private static bool TryParseTime(string value, out TimeSpan time)
	{
		if (DateTime.TryParseExact(
			    value,
			    ["HH:mm", "H:mm", "hh:mm tt", "h:mm tt", "hh:mmtt", "h:mmtt", "HH:mm:ss", "hh:mm:ss tt", "h:mm:ss tt"],
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
