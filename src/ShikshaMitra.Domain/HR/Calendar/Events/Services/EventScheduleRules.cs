using System;
using System.Text.RegularExpressions;

namespace ShikshaMitra.HR.Calendar.Events;

internal static class EventScheduleRules
{
	private static readonly Regex TimeFormatRegex = new(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", RegexOptions.Compiled);

	public static EventStatus ResolveStatus(
		DateTimeOffset? meetingDate,
		DateTimeOffset? startDate,
		DateTimeOffset? endDate,
		DateTimeOffset now)
	{
		var effectiveStartDate = meetingDate ?? startDate;
		var effectiveEndDate = meetingDate ?? endDate ?? effectiveStartDate;

		if (!effectiveStartDate.HasValue)
		{
			return EventStatus.Active;
		}

		if (effectiveEndDate!.Value.Date < now.Date)
		{
			return EventStatus.Completed;
		}

		if (effectiveStartDate.Value.Date > now.Date)
		{
			return EventStatus.Upcoming;
		}

		return EventStatus.Active;
	}

	public static EventStatus ResolveStatus(DateTimeOffset? meetingDate, DateTimeOffset now) =>
		ResolveStatus(meetingDate, null, null, now);

	public static void ValidateTime(string startTime, string endTime)
	{
		if (!TimeFormatRegex.IsMatch(startTime))
		{
			throw new ArgumentException($"Invalid start time format: {startTime}. Expected HH:mm");
		}

		if (!TimeFormatRegex.IsMatch(endTime))
		{
			throw new ArgumentException($"Invalid end time format: {endTime}. Expected HH:mm");
		}
	}
}
