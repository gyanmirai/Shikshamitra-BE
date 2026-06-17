using System;
using ShikshaMitra.HR.Policies;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public class WorkCalendarHoliday_Tests
{
	[Theory]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Utc)]
	[InlineData(DateTimeKind.Unspecified)]
	public void ToCalendarHoliday_Should_Handle_DateTime_Kinds(DateTimeKind kind)
	{
		var date = DateTime.SpecifyKind(new DateTime(2026, 5, 1), kind);
		var holiday = new WorkCalendarHoliday(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			DateOnly.FromDateTime(date),
			DateOnly.FromDateTime(date),
			"May Day",
			WorkCalendarHolidaySource.Manual,
			null,
			true,
			null);

		var result = holiday.ToCalendarHoliday(Guid.NewGuid());

		result.StartDate.ShouldBe(new DateOnly(2026, 5, 1));
		result.EndDate.ShouldBe(new DateOnly(2026, 5, 1));
	}
}
