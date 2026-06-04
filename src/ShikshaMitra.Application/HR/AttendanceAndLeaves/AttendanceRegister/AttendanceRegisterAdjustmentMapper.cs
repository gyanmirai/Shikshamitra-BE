using System;
using System.Globalization;
using ShikshaMitra.HR.Employees;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;

internal static class AttendanceRegisterAdjustmentMapper
{
	public static AttendanceRegisterAdjustmentRowResult ToUpdated(EmployeeAttendance row) =>
		new()
		{
			AttendanceId = row.Id,
			EmployeeId = row.EmployeeId,
			AttendanceDate = row.AttendanceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			Updated = true
		};

	public static AttendanceRegisterAdjustmentRowResult ToSkipped(EmployeeAttendance row, string reason) =>
		new()
		{
			AttendanceId = row.Id,
			EmployeeId = row.EmployeeId,
			AttendanceDate = row.AttendanceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			Updated = false,
			Reason = reason
		};

	public static EmployeeAttendanceStatusType ParseWireStatus(string wire) =>
		(wire ?? "").Trim().ToUpperInvariant() switch
		{
			"PRESENT" => EmployeeAttendanceStatusType.Present,
			"HALF_DAY" or "HALFDAY" or "HALF_DAY_LEAVE" => EmployeeAttendanceStatusType.HalfDayLeave,
			"ABSENT" or "FULL_DAY_LEAVE" => EmployeeAttendanceStatusType.FullDayLeave,
			_ => throw new BusinessException(HRErrorCodes.TakeAttendanceInvalidStatusWire)
		};

	public static DateTimeOffset? ParseOptionalTime(string? value) =>
		string.IsNullOrWhiteSpace(value)
			? null
			: DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
				? parsed
				: null;

	public static DateOnly ParseAdjustmentDate(string? value)
	{
		if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
		{
			return parsed;
		}

		if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
		{
			return DateOnly.FromDateTime(dto.Date);
		}

		throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);
	}

	public static DateTimeOffset? ApplyTimeToDate(DateOnly attendanceDate, DateTimeOffset? time)
	{
		if (!time.HasValue)
		{
			return null;
		}

		var dateTime = attendanceDate.ToDateTime(TimeOnly.FromDateTime(time.Value.DateTime));
		return new DateTimeOffset(dateTime, time.Value.Offset);
	}

	public static string? Truncate(string? value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.IsNullOrEmpty(value) ? value : value.Trim();
		}

		var trimmed = value.Trim();
		return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
	}
}
