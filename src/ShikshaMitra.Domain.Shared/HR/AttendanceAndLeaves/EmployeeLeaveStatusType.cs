using System;

using System.Collections.Generic;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public enum EmployeeLeaveStatusType
{
	Pending = 0,
	Approved = 1,
	Rejected = 2,
	Reviewed = 3,
	Cancelled = 4
}

public static class LeaveDurationCalculator
{
	public static decimal CountLeaveUnits(DateTime startDate, DateTime endDate, bool halfDay)
	{
		if (halfDay)
		{
			return startDate.Date == endDate.Date
				? 0.5m
				: 0m;
		}

		return CountInclusiveBusinessDays(startDate, endDate);
	}

	public static decimal CountLeaveUnits(
		DateTime startDate,
		DateTime endDate,
		bool halfDay,
		IReadOnlyDictionary<DateOnly, decimal> workingUnitsByDate)
	{
		var start = startDate.Date;
		var end = endDate.Date;
		if (start > end)
		{
			return 0m;
		}

		if (halfDay)
		{
			if (start != end)
			{
				return 0m;
			}

			var workingUnit = GetWorkingUnit(workingUnitsByDate, DateOnly.FromDateTime(start));
			return workingUnit <= 0m ? 0m : Math.Min(0.5m, workingUnit);
		}

		var units = 0m;
		for (var d = start; d <= end; d = d.AddDays(1))
		{
			units += Math.Min(1m, GetWorkingUnit(workingUnitsByDate, DateOnly.FromDateTime(d)));
		}

		return units;
	}

	public static int CountInclusiveBusinessDays(DateTime startDate, DateTime endDate)
	{
		var start = startDate.Date;
		var end = endDate.Date;
		if (start > end)
		{
			return 0;
		}

		var count = 0;
		for (var d = start; d <= end; d = d.AddDays(1))
		{
			if (d.DayOfWeek is not DayOfWeek.Sunday)
			{
				count++;
			}
		}

		return count;
	}

	public static int CountLeaveDays(DateTime startDate, DateTime endDate, bool halfDay)
	{
		return (int)Math.Ceiling(CountLeaveUnits(startDate, endDate, halfDay));
	}

	private static decimal GetWorkingUnit(IReadOnlyDictionary<DateOnly, decimal> workingUnitsByDate, DateOnly date) =>
		workingUnitsByDate.TryGetValue(date, out var unit)
			? Math.Clamp(unit, 0m, 1m)
			: 0m;
}
