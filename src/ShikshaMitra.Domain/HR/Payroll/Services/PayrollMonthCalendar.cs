using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Payroll.Services;

public static partial class PayrollMonthCalendar
{
	public static decimal CountWorkingWeekdaysExcludingHolidayWeekdays(int year, int month, IReadOnlyList<Holiday> holidays)
	{
		return CountWorkingDayUnits(
			year,
			month,
			holidays,
			SaturdayWorkPolicy.FullDay,
			Array.Empty<PayrollHolidayOverride>());
	}

	public static decimal CountWorkingDaysExcludingSundaysAndHolidays(int year, int month, IReadOnlyList<Holiday> holidays)
	{
		return CountWorkingDayUnits(
			year,
			month,
			holidays,
			SaturdayWorkPolicy.FullDay,
			Array.Empty<PayrollHolidayOverride>());
	}

	public static decimal CountWorkingDayUnits(
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		SaturdayWorkPolicy saturdayWorkPolicy,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides)
	{
		return BuildWorkingUnitsByDate(year, month, holidays, saturdayWorkPolicy, holidayOverrides).Values.Sum();
	}

	public static Dictionary<DateOnly, decimal> BuildWorkingUnitsByDate(
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		SaturdayWorkPolicy saturdayWorkPolicy,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides)
	{
		return BuildWorkingUnitsByDate(
			year,
			month,
			holidays,
			saturdayWorkPolicy,
			holidayOverrides,
			AttendanceAndLeaves.Settings.AttendanceWeeklyOffPattern.SaturdaySunday,
			Array.Empty<int>());
	}

	public static Dictionary<DateOnly, decimal> BuildWorkingUnitsByDate(
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		SaturdayWorkPolicy saturdayWorkPolicy,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides,
		WorkCalendarPolicy workCalendarPolicy)
	{
		return BuildWorkingUnitsByDate(
			year,
			month,
			holidays,
			saturdayWorkPolicy,
			holidayOverrides,
			workCalendarPolicy.WeeklyOffPattern,
			workCalendarPolicy.GetCustomWeeklyOffs());
	}

	private static Dictionary<DateOnly, decimal> BuildWorkingUnitsByDate(
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		SaturdayWorkPolicy saturdayWorkPolicy,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides,
		AttendanceAndLeaves.Settings.AttendanceWeeklyOffPattern weeklyOffPattern,
		IReadOnlyList<int> customWeeklyOffs)
	{
		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var holidayDates = GetResolvedHolidayDaysInMonth(year, month, holidays, holidayOverrides)
			.Select(x => x.Date)
			.ToHashSet();
		var units = new Dictionary<DateOnly, decimal>();

		for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
		{
			var dateOnly = DateOnly.FromDateTime(date);
			var unit = GetBaseWorkingUnit(date.DayOfWeek, saturdayWorkPolicy, weeklyOffPattern, customWeeklyOffs);
			if (unit > 0m && holidayDates.Contains(dateOnly))
			{
				unit = 0m;
			}

			units[dateOnly] = unit;
		}

		return units;
	}

	private static decimal GetBaseWorkingUnit(
		DayOfWeek dayOfWeek,
		SaturdayWorkPolicy saturdayWorkPolicy,
		AttendanceAndLeaves.Settings.AttendanceWeeklyOffPattern weeklyOffPattern,
		IReadOnlyList<int> customWeeklyOffs)
	{
		if (IsWeeklyOff(dayOfWeek, weeklyOffPattern, customWeeklyOffs))
		{
			return 0m;
		}

		return dayOfWeek switch
		{
			DayOfWeek.Saturday => saturdayWorkPolicy switch
			{
				SaturdayWorkPolicy.FullDay => 1m,
				SaturdayWorkPolicy.HalfDay => 0.5m,
				SaturdayWorkPolicy.Off => 0m,
				_ => 1m
			},
			_ => 1m
		};
	}

	public static bool IsWeeklyOff(
		DayOfWeek dayOfWeek,
		AttendanceAndLeaves.Settings.AttendanceWeeklyOffPattern weeklyOffPattern,
		IReadOnlyList<int> customWeeklyOffs)
	{
		return weeklyOffPattern switch
		{
			AttendanceAndLeaves.Settings.AttendanceWeeklyOffPattern.Sunday => dayOfWeek == DayOfWeek.Sunday,
			AttendanceAndLeaves.Settings.AttendanceWeeklyOffPattern.SaturdaySunday => dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
			AttendanceAndLeaves.Settings.AttendanceWeeklyOffPattern.Custom => customWeeklyOffs.Contains((int) dayOfWeek),
			_ => dayOfWeek == DayOfWeek.Sunday
		};
	}
}