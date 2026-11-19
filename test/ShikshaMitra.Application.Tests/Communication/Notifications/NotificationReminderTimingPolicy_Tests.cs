using System;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationReminderTimingPolicy_Tests
{
	private static readonly TimeZoneInfo SchoolZone = TimeZoneInfo.CreateCustomTimeZone("Test school", TimeSpan.FromHours(5.5), "Test school", "Test school");
	private static readonly NotificationRuleTimingConfiguration DefaultTiming = NotificationRuleTimingConfiguration.Default;
	private readonly NotificationReminderTimingPolicy _policy = new();

	[Fact]
	public void Preceding_school_day_skips_weekends_and_closures_in_the_school_timezone()
	{
		var deadline = Utc(2026, 11, 22, 20, 30);
		var days = new NotificationWorkingDays(new(2026, 11, 1), new(2026, 11, 30),
			[DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
			[new(new(2026, 11, 1), new(2026, 11, 30))], [new(new(2026, 11, 20), DayType.Holiday)]);

		_policy.ResolveSchoolDay(deadline, deadline, SchoolZone, DefaultTiming, days).ShouldBe(Utc(2026, 11, 19, 11, 30));
	}

	[Fact]
	public void An_explicit_half_day_counts_but_a_closure_takes_precedence()
	{
		var deadline = Utc(2026, 11, 23, 12, 0);
		var start = new DateOnly(2026, 11, 1);
		var halfDay = new NotificationCalendarDay(new(2026, 11, 21), DayType.HalfDay);
		var days = new NotificationWorkingDays(start, new(2026, 11, 30), [DayOfWeek.Friday], [], [halfDay]);

		_policy.ResolveSchoolDay(deadline, deadline, SchoolZone, DefaultTiming, days).ShouldBe(Utc(2026, 11, 21, 11, 30));
		var closed = days with { CalendarDays = [halfDay, halfDay with { DayType = DayType.EmergencyClosure }] };
		_policy.ResolveSchoolDay(deadline, deadline, SchoolZone, DefaultTiming, closed).ShouldBe(Utc(2026, 11, 20, 11, 30));
	}

	[Fact]
	public void Missing_working_days_and_send_times_at_or_after_expiry_produce_no_reminder()
	{
		var deadline = Utc(2026, 11, 23, 10, 0);
		var days = new NotificationWorkingDays(new(2026, 11, 23), new(2026, 11, 30), [], [], []);
		var sameDay = DefaultTiming with { ReminderSchoolDaysBefore = 0 };

		_policy.ResolveSchoolDay(deadline, deadline, SchoolZone, sameDay, days).ShouldBeNull();
		_policy.ResolveSchoolDay(deadline, deadline, SchoolZone, sameDay, days with { Weekdays = [DayOfWeek.Monday] }).ShouldBeNull();
	}

	[Fact]
	public void Fixed_occurrences_keep_the_original_instant_and_respect_expiry()
	{
		var occurrence = new DateTimeOffset(2026, 11, 22, 16, 30, 0, TimeSpan.FromHours(5.5));
		_policy.ResolveFixed(occurrence, occurrence.AddHours(1)).ShouldBe(Utc(2026, 11, 22, 11, 0));
		_policy.ResolveFixed(occurrence, occurrence).ShouldBeNull();
	}

	[Theory]
	[InlineData(20, 11, 29, 1, 20)]
	[InlineData(20, 11, 30, 1, 24)]
	[InlineData(23, 10, 0, 1, 24)]
	[InlineData(20, 11, 30, 2, 25)]
	[InlineData(24, 11, 30, 0, 24)]
	public void Daily_timetable_selects_the_due_working_date_at_the_local_cutoff(int today, int hour, int minute,
		int schoolDaysBefore, int expected)
	{
		var days = new NotificationWorkingDays(new(2026, 11, 1), new(2026, 11, 30),
			[DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
			[], [new(new(2026, 11, 23), DayType.EmergencyClosure)]);
		var config = DefaultTiming with { ReminderSchoolDaysBefore = schoolDaysBefore };
		var due = TimetableDayReminder.FindDueSchoolDate(new(2026, 11, today), Utc(2026, 11, today, hour, minute),
			config, SchoolZone, days);
		due.ShouldBe(new DateOnly(2026, 11, expected));
	}

	[Fact]
	public void A_skipped_local_reminder_time_advances_to_the_first_valid_minute()
	{
		var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
		var deadline = Utc(2026, 3, 9, 0, 0);
		var days = new NotificationWorkingDays(new(2026, 3, 1), new(2026, 3, 31), [DayOfWeek.Sunday], [], []);
		var sameDay = DefaultTiming with { ReminderSchoolDaysBefore = 0, ReminderLocalTime = new(2, 30) };

		_policy.ResolveSchoolDay(deadline, deadline, zone, sameDay, days).ShouldBe(Utc(2026, 3, 8, 7, 0));
	}

	private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
		new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
