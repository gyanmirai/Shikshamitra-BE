using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Payroll.Services;

public sealed record PayrollBasisDaysResult(
	int BasisDays,
	DayCalculationType EffectiveMode,
	bool UsedTenantDefault);

public static class PayrollMonthCalendar
{
	public static int CountWorkingWeekdaysExcludingHolidayWeekdays(int year, int month, IReadOnlyList<Holiday> holidays)
	{
		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var businessDays = 0;
		for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
		{
			if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
			{
				businessDays++;
			}
		}

		var startDate = new DateTimeOffset(firstDay);
		var endDate = new DateTimeOffset(lastDay.AddDays(1).AddTicks(-1));
		var holidayDates = new HashSet<DateTime>();
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
				if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
				{
					holidayDates.Add(date);
				}
			}
		}

		return Math.Max(0, businessDays - holidayDates.Count);
	}
}

public static class PayrollBasisDays
{
	public static PayrollBasisDaysResult Resolve(
		DayCalculationSetting? setting,
		int year,
		int month,
		int workingWeekdaysMinusHolidayWeekdays)
	{
		if (setting is null)
		{
			return new PayrollBasisDaysResult(
				workingWeekdaysMinusHolidayWeekdays,
				DayCalculationType.WorkingDays,
				UsedTenantDefault: true);
		}

		return setting.DayCalculationType switch
		{
			DayCalculationType.WorkingDays => new PayrollBasisDaysResult(
				workingWeekdaysMinusHolidayWeekdays,
				DayCalculationType.WorkingDays,
				UsedTenantDefault: false),

			DayCalculationType.TotalMonthDays => new PayrollBasisDaysResult(
				DateTime.DaysInMonth(year, month),
				DayCalculationType.TotalMonthDays,
				UsedTenantDefault: false),

			DayCalculationType.ManualDays => new PayrollBasisDaysResult(
				setting.ManualDays ?? throw new InvalidOperationException("ManualDays must be set when mode is ManualDays."),
				DayCalculationType.ManualDays,
				UsedTenantDefault: false),

			_ => throw new ArgumentOutOfRangeException(nameof(setting), setting.DayCalculationType, "Unknown day calculation type.")
		};
	}
}

public static class PayrollProrationCalculator
{
	public static decimal CalculatePayableDays(int presentDays, int halfWorkingDays) =>
		presentDays + 0.5m * halfWorkingDays;

	public static decimal CalculateProrationFactor(int basisDays, decimal payableDays)
	{
		if (basisDays <= 0)
		{
			return 1m;
		}

		return Math.Min(1m, payableDays / basisDays);
	}

	public static decimal ProrateAmount(decimal amount, bool isTemp, decimal factor)
	{
		if (isTemp)
		{
			return amount;
		}

		return Math.Round(amount * factor, 2, MidpointRounding.AwayFromZero);
	}
}

public static class PayrollDayCalculationApiLabels
{
	public static string ToApiString(DayCalculationType type) => type switch
	{
		DayCalculationType.WorkingDays => "working-days",
		DayCalculationType.TotalMonthDays => "total-month-days",
		DayCalculationType.ManualDays => "manual-days",
		_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
	};
}