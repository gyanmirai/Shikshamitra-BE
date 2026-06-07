using System;
using System.Collections.Generic;
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

	[Fact]
	public void CountLeaveUnits_With_WorkingUnits_Uses_Calendar_Units()
	{
		var workingUnits = new Dictionary<DateOnly, decimal>
		{
			[new(2026, 6, 6)] = 0m,
			[new(2026, 6, 7)] = 0m,
			[new(2026, 6, 8)] = 1m,
			[new(2026, 6, 9)] = 0.5m
		};

		var units = LeaveDurationCalculator.CountLeaveUnits(
			new DateTime(2026, 6, 6),
			new DateTime(2026, 6, 9),
			halfDay: false,
			workingUnits);

		Assert.Equal(1.5m, units);
	}

	[Fact]
	public void CountLeaveUnits_HalfDay_With_WorkingUnits_Rejects_NonWorking_Date()
	{
		var workingUnits = new Dictionary<DateOnly, decimal>
		{
			[new(2026, 6, 6)] = 0m
		};

		var units = LeaveDurationCalculator.CountLeaveUnits(
			new DateTime(2026, 6, 6),
			new DateTime(2026, 6, 6),
			halfDay: true,
			workingUnits);

		Assert.Equal(0m, units);
	}
}
