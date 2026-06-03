using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Dashboard;

internal static class HRDashboardCelebrationProjection
{
	public static HRDashboardCelebrationsDto BuildCelebrations(
		IReadOnlyCollection<ActiveEmployeeDashboardRow> employees,
		DateTime dashboardDate)
	{
		var celebrations = employees
			.SelectMany(e => BuildCelebrationCandidates(e, dashboardDate, 30))
			.OrderBy(c => c.Date)
			.ThenBy(c => c.EmployeeName)
			.ToList();
		var today = celebrations
			.Where(c => c.Date.Date == dashboardDate.Date)
			.OrderBy(c => c.EmployeeName)
			.ToList();
		var upcoming = celebrations
			.Where(c => c.Date.Date > dashboardDate.Date)
			.Take(12)
			.ToList();

		return new HRDashboardCelebrationsDto
		{
			Today = today,
			Upcoming = upcoming
		};
	}

	private static IEnumerable<HRDashboardCelebrationDto> BuildCelebrationCandidates(
		ActiveEmployeeDashboardRow employee,
		DateTime dashboardDate,
		int daysAhead)
	{
		var rangeStart = dashboardDate.Date;
		var rangeEnd = rangeStart.AddDays(daysAhead);
		if (employee.DateOfBirth.HasValue)
		{
			var birthday = GetNextMonthDayOccurrence(
				employee.DateOfBirth.Value.Month,
				employee.DateOfBirth.Value.Day,
				rangeStart);
			if (birthday <= rangeEnd)
			{
				yield return new HRDashboardCelebrationDto
				{
					EmployeeId = employee.Id,
					EmployeeName = employee.EmployeeName,
					EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
					Type = "Birthday",
					Date = birthday
				};
			}
		}

		if (employee.DateOfJoining.HasValue)
		{
			var anniversary = GetNextMonthDayOccurrence(
				employee.DateOfJoining.Value.Month,
				employee.DateOfJoining.Value.Day,
				rangeStart);
			var years = anniversary.Year - employee.DateOfJoining.Value.Year;
			if (anniversary <= rangeEnd && years > 0)
			{
				yield return new HRDashboardCelebrationDto
				{
					EmployeeId = employee.Id,
					EmployeeName = employee.EmployeeName,
					EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
					Type = "Work Anniversary",
					Date = anniversary,
					Years = years
				};
			}
		}
	}

	private static DateTime GetNextMonthDayOccurrence(int month, int day, DateTime fromDate)
	{
		var normalizedDay = Math.Min(day, DateTime.DaysInMonth(fromDate.Year, month));
		var occurrence = new DateTime(fromDate.Year, month, normalizedDay);
		if (occurrence >= fromDate.Date)
		{
			return occurrence;
		}

		var nextYearDay = Math.Min(day, DateTime.DaysInMonth(fromDate.Year + 1, month));
		return new DateTime(fromDate.Year + 1, month, nextYearDay);
	}
}
