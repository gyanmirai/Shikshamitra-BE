using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Students.Attendance;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class AttendanceCalendar_Tests
{
	private static readonly DateTime Sunday = new(2026, 10, 4);
	[Fact]
	public void Current_day_without_calendar_can_be_marked_but_explicit_closures_remain_excluded()
	{
		var missing = new AttendanceApplicabilityContext(new(), new Dictionary<DateTime, AttendanceApplicabilityResult>());
		missing.For(Guid.Empty, Sunday, Sunday).IsExpected.ShouldBeTrue();
		missing.For(Guid.Empty, Sunday, Sunday).IsProvisional.ShouldBeTrue();
		missing.For(Guid.Empty, Sunday, Sunday.AddDays(1)).IsExpected.ShouldBeFalse();
		var unconfigured = new AttendanceApplicabilityContext(new(), new Dictionary<DateTime, AttendanceApplicabilityResult>
		{
			[Sunday] = new(false, "Working day not configured", IsProvisional: true)
		});
		unconfigured.For(Guid.Empty, Sunday, Sunday).IsExpected.ShouldBeTrue();
		var closure = new AttendanceApplicabilityContext(new(), new Dictionary<DateTime, AttendanceApplicabilityResult>
		{
			[Sunday] = new(false, "Weather closure")
		});
		closure.For(Guid.Empty, Sunday, Sunday).IsExpected.ShouldBeFalse();
	}
	[Fact]
	public void Unpublished_or_missing_calendar_never_assumes_a_school_day()
	{
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [], [], null).Reason.ShouldBe("Calendar not configured");
		var draft = Calendar(false);
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [draft], [], Setting()).IsExpected.ShouldBeFalse();
	}
	[Fact]
	public void Missing_calendar_is_provisional_while_a_confirmed_holiday_is_final()
	{
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [], [], null).IsProvisional.ShouldBeTrue();
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [Calendar(), Calendar()], [], Setting()).IsProvisional.ShouldBeTrue();
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [Calendar()], [], null).IsProvisional.ShouldBeTrue();
		var calendar = Calendar();
		var holiday = AttendanceCalendarDayEvaluator.Resolve(Sunday, [calendar], [Day(calendar, DayType.Holiday)], Setting());
		holiday.IsExpected.ShouldBeFalse(); holiday.IsProvisional.ShouldBeFalse();
	}
	[Fact]
	public void Special_working_Sunday_overrides_calendar_weekend()
	{
		var calendar = Calendar(); var working = Day(calendar, DayType.WorkingDay);
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [calendar], [working], Setting()).IsExpected.ShouldBeTrue();
	}
	[Fact]
	public void Emergency_closure_overrides_special_working_day()
	{
		var calendar = Calendar(); var working = Day(calendar, DayType.WorkingDay); var closed = Day(calendar, DayType.EmergencyClosure);
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [calendar], [working, closed], Setting()).IsExpected.ShouldBeFalse();
	}
	[Fact]
	public void Cancelled_closure_does_not_exempt_a_working_day()
	{
		var calendar = Calendar(); var working = Day(calendar, DayType.WorkingDay); var closed = Day(calendar, DayType.EmergencyClosure); closed.Cancel("Reopened");
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [calendar], [working, closed], Setting()).IsExpected.ShouldBeTrue();
	}
	[Fact]
	public void Whole_school_half_day_is_one_full_expected_daily_record()
	{
		var calendar = Calendar(); var halfDay = Day(calendar, DayType.HalfDay);
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [calendar], [halfDay], Setting()).IsExpected.ShouldBeTrue();
	}
	[Fact]
	public void Ambiguous_published_calendars_block_marking()
	{
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [Calendar(), Calendar()], [], Setting()).Reason.ShouldBe("Conflicting published calendars");
	}
	[Fact]
	public void Custom_weekend_requires_explicit_working_day()
	{
		var setting = Setting(); setting.WeekendRule = WeekendRule.Custom;
		AttendanceCalendarDayEvaluator.Resolve(Sunday.AddDays(1), [Calendar()], [], setting).Reason.ShouldBe("Working day not configured");
	}
	[Fact]
	public void Existing_calendar_can_require_attendance_on_holidays_and_vacations()
	{
		var calendar = Calendar(); var setting = Setting(); var holiday = Day(calendar, DayType.Holiday); var vacation = Day(calendar, DayType.Vacation);
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [calendar], [holiday], setting).IsExpected.ShouldBeFalse();
		setting.AutoExemptAttendanceOnHoliday = false; setting.AutoExemptAttendanceOnVacation = false;
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [calendar], [holiday], setting).IsExpected.ShouldBeTrue();
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [calendar], [vacation], setting).IsExpected.ShouldBeTrue();
		AttendanceCalendarDayEvaluator.Resolve(Sunday, [calendar], [Day(calendar, DayType.EmergencyClosure)], setting).IsExpected.ShouldBeFalse();
	}
	private static AcademicCalendar Calendar(bool published = true)
	{
		var c = new AcademicCalendar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "School calendar", Sunday.AddDays(-30), Sunday.AddDays(30));
		if (published) c.Publish(Guid.NewGuid()); return c;
	}
	private static AcademicCalendarDay Day(AcademicCalendar calendar, DayType type) => new(Guid.NewGuid(), calendar.TenantId, calendar.Id, Sunday, type, type.ToString());
	private static AcademicCalendarSetting Setting() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
