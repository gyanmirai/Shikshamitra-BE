using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.SalarySlips;

namespace ShikshaMitra.HR.Payroll.Services;

public sealed record PayrollComputedLineItem(
	EmployeePayrollComponent Source,
	decimal Amount);

public sealed record PayrollComputationResult(
	int PresentDays,
	int HalfWorkingDays,
	decimal PayableDays,
	decimal ProrationFactor,
	decimal BasicSalary,
	decimal FullGrossSalary,
	decimal GrossSalary,
	decimal TotalDeduction,
	decimal NetPay,
	List<PayrollComputedLineItem> Earnings,
	List<PayrollComputedLineItem> Deductions);

public sealed record PayrollAttendancePayableDays(
	int PresentDays,
	int HalfWorkingDays,
	decimal PayableDays);

public sealed record PayrollPaidLeaveDay(
	DateOnly Date,
	decimal PayableUnits);

public sealed record PayrollLeaveUnits(
	decimal PaidUnits,
	decimal UnpaidUnits);

public sealed record PayrollBasisDaysResult(
	int BasisDays,
	DayCalculationType EffectiveMode,
	bool UsedTenantDefault);

public static class PayrollMonthCalendar
{
	public static int CountWorkingWeekdaysExcludingHolidayWeekdays(int year, int month, IReadOnlyList<Holiday> holidays)
	{
		return CountWorkingDaysExcludingSundaysAndHolidays(year, month, holidays);
	}

	public static int CountWorkingDaysExcludingSundaysAndHolidays(int year, int month, IReadOnlyList<Holiday> holidays)
	{
		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var businessDays = 0;
		for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
		{
			if (date.DayOfWeek != DayOfWeek.Sunday)
			{
				businessDays++;
			}
		}

		var holidayDates = GetHolidayDatesInMonth(year, month, holidays)
			.Where(date => date.DayOfWeek != DayOfWeek.Sunday)
			.ToHashSet();

		return Math.Max(0, businessDays - holidayDates.Count);
	}

	public static HashSet<DateOnly> GetHolidayDateOnlySetInMonth(int year, int month, IReadOnlyList<Holiday> holidays) =>
		GetHolidayDatesInMonth(year, month, holidays)
			.Select(DateOnly.FromDateTime)
			.ToHashSet();

	private static IEnumerable<DateTime> GetHolidayDatesInMonth(int year, int month, IReadOnlyList<Holiday> holidays)
	{
		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
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
				holidayDates.Add(date);
			}
		}

		return holidayDates;
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
				setting.GetManualDaysForMonth(year, month)
					?? throw new InvalidOperationException($"ManualDays must be set for {year}-{month:00} when mode is ManualDays."),
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

public static class PayrollAttendanceCalculator
{
	public static PayrollAttendancePayableDays CalculatePayableDays(
		IReadOnlyList<EmployeeAttendance> attendanceRecords,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate = null)
	{
		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var monthRecords = attendanceRecords
			.Where(a => a.AttendanceDate >= startDate && a.AttendanceDate <= endDate)
			.GroupBy(a => a.AttendanceDate)
			.Select(g => g.Last())
			.ToList();

		if (basis.EffectiveMode == DayCalculationType.TotalMonthDays)
		{
			var presentDays = monthRecords.Count(a => a.StatusType == EmployeeAttendanceStatusType.Present);
			var halfDays = monthRecords.Count(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);
			var unpaidDeduction = monthRecords.Sum(a =>
			{
				var leaveUnits = GetLeaveUnits(a.AttendanceDate, leaveUnitsByDate);
				return a.StatusType switch
				{
					EmployeeAttendanceStatusType.FullDayLeave => Math.Max(0m, 1m - leaveUnits.PaidUnits),
					EmployeeAttendanceStatusType.HalfDayLeave => Math.Max(0m, 0.5m - leaveUnits.PaidUnits),
					_ => 0m
				};
			});
			var attendanceDates = monthRecords
				.Where(a => a.StatusType == EmployeeAttendanceStatusType.Present)
				.Select(a => a.AttendanceDate)
				.ToHashSet();
			if (leaveUnitsByDate is { Count: > 0 })
			{
				foreach (var (date, leaveUnits) in leaveUnitsByDate)
				{
					if (date < startDate || date > endDate || attendanceDates.Contains(date))
					{
						continue;
					}

					var existingDeduction = monthRecords
						.Where(a => a.AttendanceDate == date)
						.Sum(a => a.StatusType switch
						{
							EmployeeAttendanceStatusType.FullDayLeave => Math.Max(0m, 1m - GetLeaveUnits(date, leaveUnitsByDate).PaidUnits),
							EmployeeAttendanceStatusType.HalfDayLeave => Math.Max(0m, 0.5m - GetLeaveUnits(date, leaveUnitsByDate).PaidUnits),
							_ => 0m
						});
					unpaidDeduction += Math.Max(0m, Math.Clamp(leaveUnits.UnpaidUnits, 0m, 1m) - existingDeduction);
				}
			}
			var totalMonthPayableDays = Math.Max(0m, basis.BasisDays - unpaidDeduction);
			return new PayrollAttendancePayableDays(presentDays, halfDays, totalMonthPayableDays);
		}

		var holidayDates = PayrollMonthCalendar.GetHolidayDateOnlySetInMonth(year, month, holidays);
		if (basis.EffectiveMode == DayCalculationType.WorkingDays)
		{
			monthRecords = monthRecords
				.Where(a => IsWorkingDay(a.AttendanceDate, holidayDates))
				.ToList();
		}

		var present = monthRecords.Count(a => a.StatusType == EmployeeAttendanceStatusType.Present);
		var half = monthRecords.Count(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);
		var attendancePayableByDate = monthRecords.ToDictionary(
			a => a.AttendanceDate,
			a => a.StatusType switch
			{
				EmployeeAttendanceStatusType.Present => 1m,
				EmployeeAttendanceStatusType.HalfDayLeave => 0.5m,
				_ => 0m
			});

		var payableDays = attendancePayableByDate.Values.Sum();
		if (leaveUnitsByDate is { Count: > 0 })
		{
			foreach (var (date, leaveUnits) in leaveUnitsByDate)
			{
				if (date < startDate || date > endDate)
				{
					continue;
				}

				if (basis.EffectiveMode == DayCalculationType.WorkingDays && !IsWorkingDay(date, holidayDates))
				{
					continue;
				}

				var paidUnitsForDate = Math.Clamp(leaveUnits.PaidUnits, 0m, 1m);
				var attendanceUnits = attendancePayableByDate.GetValueOrDefault(date);
				if (paidUnitsForDate > attendanceUnits)
				{
					payableDays += paidUnitsForDate - attendanceUnits;
				}
			}
		}

		return new PayrollAttendancePayableDays(
			present,
			half,
			payableDays);
	}

	private static bool IsWorkingDay(DateOnly date, IReadOnlySet<DateOnly> holidayDates) =>
		date.DayOfWeek != DayOfWeek.Sunday && !holidayDates.Contains(date);

	private static PayrollLeaveUnits GetLeaveUnits(
		DateOnly date,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate)
	{
		if (leaveUnitsByDate is null || !leaveUnitsByDate.TryGetValue(date, out var units))
		{
			return new PayrollLeaveUnits(0m, 0m);
		}

		return new PayrollLeaveUnits(
			Math.Clamp(units.PaidUnits, 0m, 1m),
			Math.Clamp(units.UnpaidUnits, 0m, 1m));
	}
}

public static class PayrollComputation
{
	public static PayrollComputationResult Calculate(
		int presentDays,
		int halfWorkingDays,
		int basisDays,
		IReadOnlyList<EmployeePayrollComponent> components)
	{
		var payableDays = PayrollProrationCalculator.CalculatePayableDays(presentDays, halfWorkingDays);
		return Calculate(presentDays, halfWorkingDays, payableDays, basisDays, components);
	}

	public static PayrollComputationResult Calculate(
		int presentDays,
		int halfWorkingDays,
		decimal payableDays,
		int basisDays,
		IReadOnlyList<EmployeePayrollComponent> components)
	{
		var factor = PayrollProrationCalculator.CalculateProrationFactor(basisDays, payableDays);
		var earnings = new List<PayrollComputedLineItem>();
		var deductions = new List<PayrollComputedLineItem>();
		var fullGrossSalary = components
			.Where(c => c.IsEarning)
			.Sum(c => c.Amount);
		var grossSalary = 0m;
		var totalDeduction = 0m;

		foreach (var component in components)
		{
			var proratedAmount = PayrollProrationCalculator.ProrateAmount(component.Amount, component.IsTemp, factor);
			var lineItem = new PayrollComputedLineItem(component, proratedAmount);
			if (component.IsEarning)
			{
				earnings.Add(lineItem);
				grossSalary += proratedAmount;
			}
			else
			{
				deductions.Add(lineItem);
				totalDeduction += proratedAmount;
			}
		}

		var basicSalaryBase = components
			.Where(c => c.IsEarning && IsBasicSalaryComponentName(c.Name))
			.Sum(c => c.Amount);
		var basicSalary = PayrollProrationCalculator.ProrateAmount(basicSalaryBase, isTemp: false, factor);

		return new PayrollComputationResult(
			presentDays,
			halfWorkingDays,
			payableDays,
			factor,
			basicSalary,
			fullGrossSalary,
			grossSalary,
			totalDeduction,
			grossSalary - totalDeduction,
			earnings,
			deductions);
	}

	public static SalarySlipLineItem ToSalarySlipLineItem(PayrollComputedLineItem item) =>
		new(
			item.Source.Name,
			item.Amount,
			item.Source.Description,
			item.Source.IsFixed,
			item.Source.IsTemp,
			item.Source.ComponentType,
			item.Source.DependOn ?? SalaryComponentDependency.None);

	private static bool IsBasicSalaryComponentName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}

		return string.Equals(name, "Basic", StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase);
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
