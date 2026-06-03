using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	internal readonly record struct SeedHolidayDateRange(DateOnly StartDate, DateOnly EndDate);

	internal static HashSet<DateOnly> BuildSeedHolidayDateSet(
		IEnumerable<SeedHolidayDateRange> holidays,
		DateOnly windowStart,
		DateOnly windowEnd)
	{
		var holidayDates = new HashSet<DateOnly>();
		if (windowEnd < windowStart)
		{
			return holidayDates;
		}

		foreach (var holiday in holidays)
		{
			var start = holiday.StartDate < windowStart ? windowStart : holiday.StartDate;
			var end = holiday.EndDate > windowEnd ? windowEnd : holiday.EndDate;
			if (end < start)
			{
				continue;
			}

			for (var date = start; date <= end; date = date.AddDays(1))
			{
				holidayDates.Add(date);
			}
		}

		return holidayDates;
	}

	internal static bool ShouldSeedAttendanceForDate(DateOnly date, IReadOnlySet<DateOnly> holidayDates) =>
		date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) &&
		!holidayDates.Contains(date);

	private static decimal RoundToNearest(decimal amount, decimal nearest)
	{
		return Math.Round(amount / nearest, MidpointRounding.AwayFromZero) * nearest;
	}

	private static SalaryComponentDependency? MapSalaryDependency(ComponentDependency dependOn)
	{
		return dependOn switch
		{
			ComponentDependency.None => SalaryComponentDependency.None,
			ComponentDependency.Basic => SalaryComponentDependency.Basic,
			ComponentDependency.Gross => SalaryComponentDependency.Gross,
			_ => null
		};
	}
}
