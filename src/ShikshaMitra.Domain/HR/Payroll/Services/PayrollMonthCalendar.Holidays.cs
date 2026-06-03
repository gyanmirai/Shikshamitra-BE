using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Payroll.Services;

public static partial class PayrollMonthCalendar
{
	public static HashSet<DateOnly> GetHolidayDateOnlySetInMonth(int year, int month, IReadOnlyList<Holiday> holidays) =>
		GetHolidayDatesInMonth(year, month, holidays)
			.Select(DateOnly.FromDateTime)
			.ToHashSet();

	public static IReadOnlyList<PayrollResolvedHolidayDay> GetResolvedHolidayDaysInMonth(
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides)
	{
		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var startDate = new DateTimeOffset(firstDay);
		var endDate = new DateTimeOffset(lastDay.AddDays(1).AddTicks(-1));
		var resolved = new Dictionary<(DateOnly Date, Guid? CalendarHolidayId), PayrollResolvedHolidayDay>();

		foreach (var holiday in holidays)
		{
			if (holiday.StartDate > endDate || holiday.EndDate < startDate)
			{
				continue;
			}

			var hStart = holiday.StartDate.Date > firstDay ? holiday.StartDate.Date : firstDay;
			var hEnd = holiday.EndDate.Date < lastDay ? holiday.EndDate.Date : lastDay;
			for (var date = hStart; date <= hEnd; date = date.AddDays(1))
			{
				var key = (DateOnly.FromDateTime(date), (Guid?)holiday.Id);
				resolved[key] = new PayrollResolvedHolidayDay(
					key.Item1,
					holiday.Id,
					holiday.HolidayName,
					IncludedInPayroll: true,
					IsPayrollOnly: false);
			}
		}

		foreach (var holidayOverride in holidayOverrides)
		{
			if (holidayOverride.Date.Year != year || holidayOverride.Date.Month != month)
			{
				continue;
			}

			var date = DateOnly.FromDateTime(holidayOverride.Date.Date);
			var key = (date, holidayOverride.CalendarHolidayId);
			resolved[key] = new PayrollResolvedHolidayDay(
				date,
				holidayOverride.CalendarHolidayId,
				holidayOverride.HolidayName,
				holidayOverride.IncludedInPayroll,
				IsPayrollOnly: holidayOverride.CalendarHolidayId == null);
		}

		return resolved.Values
			.OrderBy(x => x.Date)
			.ThenBy(x => x.HolidayName)
			.ToList();
	}

	private static IEnumerable<DateTime> GetHolidayDatesInMonth(int year, int month, IReadOnlyList<Holiday> holidays)
	{
		var resolved = GetResolvedHolidayDaysInMonth(
			year,
			month,
			holidays,
			Array.Empty<PayrollHolidayOverride>());
		return resolved.Select(x => x.Date.ToDateTime(TimeOnly.MinValue));
	}
}
