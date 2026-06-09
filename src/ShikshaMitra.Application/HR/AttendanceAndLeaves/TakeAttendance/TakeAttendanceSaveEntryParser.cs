using System;
using System.Globalization;
using ShikshaMitra.Common;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

public static class TakeAttendanceSaveEntryParser
{
	public static ParsedTakeAttendanceEntry Parse(TakeAttendanceSaveEntryDto entry) =>
		new(entry, ParseWireStatus(entry.StatusWire));

	public static DateTimeOffset? TryParseTime(string? value, DateOnly attendanceDate)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
		{
			return new DateTimeOffset(attendanceDate.ToDateTime(parsedTime), TimeSpan.Zero);
		}

		if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedOffset))
		{
			return new DateTimeOffset(attendanceDate.ToDateTime(TimeOnly.FromTimeSpan(parsedOffset.TimeOfDay)), parsedOffset.Offset);
		}

		if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime))
		{
			return new DateTimeOffset(attendanceDate.ToDateTime(TimeOnly.FromDateTime(parsedDateTime)), TimeSpan.Zero);
		}

		return null;
	}

	public static string? TruncateForStorage(string? value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.IsNullOrEmpty(value) ? value : value.Trim();
		}

		var trimmed = value.Trim();
		return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
	}

	private static EmployeeAttendanceStatusType ParseWireStatus(string wire) =>
		(wire ?? "").Trim().ToUpperInvariant() switch
		{
			"PRESENT" => EmployeeAttendanceStatusType.Present,
			"HALF_DAY" or "HALFDAY" or "HALF_DAY_LEAVE" => EmployeeAttendanceStatusType.HalfDayLeave,
			"ABSENT" => EmployeeAttendanceStatusType.FullDayLeave,
			_ => throw new BusinessException(HRErrorCodes.TakeAttendanceInvalidStatusWire)
		};
}
