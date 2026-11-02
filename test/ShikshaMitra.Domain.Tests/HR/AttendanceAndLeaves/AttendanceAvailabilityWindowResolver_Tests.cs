using System;
using ShikshaMitra.HR.WorkShifts;
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
		var shift = new EmployeeWorkShift(Guid.NewGuid(), Guid.NewGuid(), "Teaching shift", new TimeOnly(8, 0), new TimeOnly(15, 0));
		var window = AttendanceAvailabilityWindowResolver.Resolve(shift, isFullDay, new TimeOnly(9, 0), new TimeOnly(14, 15));
		window.Start.ShouldBe(new TimeOnly(8, 0));
		window.End.ShouldBe(TimeOnly.Parse(expectedEnd));
	}

	[Fact]
	public void Resolve_Should_Preserve_Existing_Window_For_Missing_Shift()
	{
		var window = AttendanceAvailabilityWindowResolver.Resolve(null, true, new TimeOnly(9, 0), new TimeOnly(14, 15));
		window.Start.ShouldBe(new TimeOnly(9, 0)); window.End.ShouldBe(new TimeOnly(14, 15));
	}
	[Fact]
	public void Overnight_Definitions_Should_Be_Rejected() =>
	 Should.Throw<Volo.Abp.BusinessException>(() => new EmployeeWorkShift(Guid.NewGuid(), Guid.NewGuid(), "Night", new(22, 0), new(6, 0)));
}