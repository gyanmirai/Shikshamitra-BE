using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using ShikshaMitra.HR.Employees;
using System;
using System.Globalization;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class AttendanceTrackingFormatting
{
	public static DateTime ParseDate(string dateString)
	{
		if (DateTime.TryParseExact(dateString, "dd-MM-yyyy", CultureInfo.InvariantCulture,
			    DateTimeStyles.None, out var result))
		{
			return result;
		}

		if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
		{
			return result;
		}

		if (DateTimeOffset.TryParse(dateString, out var dto))
		{
			return dto.DateTime;
		}

		throw new BusinessException(HRErrorCodes.AttendanceInvalidDateFormat);
	}

	public static string? CalculateHoursWorked(DateTimeOffset? timeIn, DateTimeOffset? timeOut)
	{
		if (!timeIn.HasValue || !timeOut.HasValue)
		{
			return null;
		}

		var duration = timeOut.Value - timeIn.Value;
		var hours = (int)duration.TotalHours;
		var minutes = duration.Minutes;
		return $"{hours:D2}:{minutes:D2}";
	}

	public static AttendanceTrackingStatus MapToTrackingStatus(EmployeeAttendanceStatusType statusType, bool hasRejectedLeave)
	{
		if (hasRejectedLeave)
		{
			return AttendanceTrackingStatus.RejectedLeave;
		}

		return statusType switch
		{
			EmployeeAttendanceStatusType.Present => AttendanceTrackingStatus.Present,
			EmployeeAttendanceStatusType.FullDayLeave => AttendanceTrackingStatus.Absent,
			EmployeeAttendanceStatusType.HalfDayLeave => AttendanceTrackingStatus.HalfDay,
			_ => AttendanceTrackingStatus.Absent
		};
	}

	public static AttendanceTrackingStatus ResolveEmployeeWiseStatus(
		EmployeeAttendance? attendance,
		bool hasRejectedLeave,
		AttendanceSummary summary)
	{
		if (hasRejectedLeave)
		{
			summary.RejectedLeave++;
			return AttendanceTrackingStatus.RejectedLeave;
		}

		if (attendance == null)
		{
			summary.Absent++;
			return AttendanceTrackingStatus.Absent;
		}

		var status = MapToTrackingStatus(attendance.StatusType, false);
		switch (status)
		{
			case AttendanceTrackingStatus.Present:
				summary.Present++;
				break;
			case AttendanceTrackingStatus.Absent:
				summary.Absent++;
				break;
			case AttendanceTrackingStatus.HalfDay:
				summary.HalfDay++;
				break;
		}

		return status;
	}

	public static bool MatchesEmployeeWiseSearch(AttendanceEmployeeWiseItem item, string? searchText)
	{
		if (string.IsNullOrWhiteSpace(searchText))
		{
			return true;
		}

		var search = searchText.Trim().ToLowerInvariant();
		return item.Day.ToLowerInvariant().Contains(search) ||
			item.Date.ToLowerInvariant().Contains(search) ||
			(item.Description ?? string.Empty).ToLowerInvariant().Contains(search) ||
			item.Status.ToString().ToLowerInvariant().Contains(search);
	}

	public static string BuildDateIsoString(DateTime date, TimeSpan? offset)
	{
		var dateOffset = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, offset ?? TimeSpan.Zero);
		return dateOffset.ToString("o");
	}

	public static string FormatEmployeeName(string? firstName, string? middleName, string? lastName)
	{
		var parts = new[] { firstName, middleName, lastName }
			.Where(s => !string.IsNullOrWhiteSpace(s));
		return string.Join(" ", parts);
	}
}
