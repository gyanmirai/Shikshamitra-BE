using System;
using System.Globalization;
using ShikshaMitra.Common;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

public static class TakeAttendanceSaveEntryParser
{
	public static ParsedTakeAttendanceEntry Parse(TakeAttendanceSaveEntryDto entry) =>
		new(entry, ParseWireStatus(entry.StatusWire));

	public static DateTimeOffset? TryParseTime(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value) &&
		    DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
		{
			return parsed;
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
