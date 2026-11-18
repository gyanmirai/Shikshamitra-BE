using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Academic.Calendar;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationCalendarRange(DateOnly StartDate, DateOnly EndDate);

public sealed record NotificationCalendarDay(DateOnly Date, DayType DayType);

public sealed record NotificationWorkingDays(DateOnly StartDate, DateOnly EndDate, IReadOnlyCollection<DayOfWeek> Weekdays,
	IReadOnlyList<NotificationCalendarRange> PublishedRanges, IReadOnlyList<NotificationCalendarDay> CalendarDays);

public sealed class NotificationReminderTimingPolicy : ITransientDependency
{
	internal const int MaximumLookbackDays = 730;

	public DateTimeOffset? ResolveFixed(DateTimeOffset occurrenceAt, DateTimeOffset? expiresAt) =>
		occurrenceAt >= expiresAt ? null : occurrenceAt.ToUniversalTime();

	public DateTimeOffset? ResolveSchoolDay(DateTimeOffset occurrenceAt, DateTimeOffset? expiresAt, TimeZoneInfo schoolZone,
		NotificationRuleTimingConfiguration timing, NotificationWorkingDays days)
	{
		var occurrenceDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurrenceAt, schoolZone).DateTime);
		var index = Math.Min(occurrenceDate.DayNumber, days.EndDate.DayNumber);
		var remaining = timing.ReminderSchoolDaysBefore;
		if (remaining > 0 && index == occurrenceDate.DayNumber) index--;
		var firstDay = Math.Max(days.StartDate.DayNumber, occurrenceDate.DayNumber - MaximumLookbackDays);
		var blocked = days.CalendarDays.Where(x => CalendarDayBlockingRules.BlocksTimetable(x.DayType)).Select(x => x.Date).ToHashSet();
		var explicitDays = days.CalendarDays.Where(x => x.DayType is DayType.WorkingDay or DayType.HalfDay).Select(x => x.Date).ToHashSet();
		for (; index >= firstDay; index--)
		{
			var date = DateOnly.FromDayNumber(index);
			if (blocked.Contains(date) || (!explicitDays.Contains(date) && !IsRegularDay(date, days))) continue;
			if (remaining > 1) { remaining--; continue; }
			var reminderAt = NotificationDeliveryTimingPolicy.Boundary(date.ToDateTime(timing.ReminderLocalTime), schoolZone, later: false);
			return reminderAt >= occurrenceAt || reminderAt >= expiresAt ? null : reminderAt;
		}
		return null;
	}

	private static bool IsRegularDay(DateOnly date, NotificationWorkingDays days) => days.Weekdays.Count > 0
		? days.Weekdays.Contains(date.DayOfWeek)
		: days.PublishedRanges.Any(x => date >= x.StartDate && date <= x.EndDate);
}
