using System;
using System.Text.RegularExpressions;

namespace ShikshaMitra.HR.Calendar.Events;

internal static class EventScheduleRules
{
	private static readonly Regex TimeFormatRegex = new(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", RegexOptions.Compiled);

	public static EventStatus ResolveStatus(DateTimeOffset? meetingDate, DateTimeOffset now)
	{
		if (!meetingDate.HasValue)
		{
			return EventStatus.Active;
		}

		if (meetingDate.Value.Date < now.Date)
		{
			return EventStatus.Completed;
		}

		return meetingDate.Value.Date == now.Date
			? EventStatus.Active
			: EventStatus.Upcoming;
	}

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
