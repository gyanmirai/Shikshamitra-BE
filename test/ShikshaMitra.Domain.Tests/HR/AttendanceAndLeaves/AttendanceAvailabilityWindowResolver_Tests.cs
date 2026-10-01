using System;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceAvailabilityWindowResolver_Tests
{
	[Theory]
	[InlineData(true, "15:00")]
	[InlineData(false, "11:30")]
	public void Resolve_Should_Use_Configured_Shift(bool isFullDay, string expectedEnd)
	{
		var shift = new ShiftManagement { ShiftName = "Teaching shift", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(15, 0) };
		var window = AttendanceAvailabilityWindowResolver.Resolve(shift, isFullDay, new TimeOnly(9, 0), new TimeOnly(14, 15));
		window.Start.ShouldBe(new TimeOnly(8, 0));
		window.End.ShouldBe(TimeOnly.Parse(expectedEnd));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Resolve_Should_Preserve_Existing_Window_For_Missing_Or_Overnight_Shift(bool overnight)
	{
		var shift = overnight
			? new ShiftManagement { ShiftName = "Night shift", StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(6, 0) }
			: null;
		var window = AttendanceAvailabilityWindowResolver.Resolve(shift, true, new TimeOnly(9, 0), new TimeOnly(14, 15));
		window.Start.ShouldBe(new TimeOnly(9, 0));
		window.End.ShouldBe(new TimeOnly(14, 15));
	}
}
