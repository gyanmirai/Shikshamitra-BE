using System;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveDurationCalculatorTests
{
	[Fact]
	public void CountLeaveUnits_HalfDay_SingleDate_Is_Point_Five()
	{
		var units = LeaveDurationCalculator.CountLeaveUnits(
			new DateTime(2026, 6, 8),
			new DateTime(2026, 6, 8),
			halfDay: true);

		Assert.Equal(0.5m, units);
	}

	[Fact]
	public void CountLeaveUnits_HalfDay_MultiDate_Is_Invalid()
	{
		var units = LeaveDurationCalculator.CountLeaveUnits(
			new DateTime(2026, 6, 8),
			new DateTime(2026, 6, 9),
			halfDay: true);

		Assert.Equal(0m, units);
	}

	[Fact]
	public void CountLeaveUnits_FullDay_Includes_Saturday_And_Excludes_Sunday()
	{
		var units = LeaveDurationCalculator.CountLeaveUnits(
			new DateTime(2026, 6, 6),
			new DateTime(2026, 6, 8),
			halfDay: false);

		Assert.Equal(2m, units);
	}
}
