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

public sealed record PayrollApprovedAttendanceDay(
	DateOnly Date,
	EmployeeAttendanceStatusType Status,
	decimal PayableUnits);

public sealed record PayrollBasisDaysResult(
	decimal BasisDays,
	DayCalculationType EffectiveMode,
	bool UsedTenantDefault,
	SaturdayWorkPolicy SaturdayWorkPolicy = SaturdayWorkPolicy.FullDay,
	IReadOnlyDictionary<DateOnly, decimal>? WorkingUnitsByDate = null);

public sealed record PayrollResolvedHolidayDay(
	DateOnly Date,
	Guid? CalendarHolidayId,
	string HolidayName,
	bool IncludedInPayroll,
	bool IsPayrollOnly);

public static class PayrollMonthCalendar
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
		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var includedHolidayDates = GetResolvedHolidayDaysInMonth(year, month, holidays, holidayOverrides)
			.Where(x => x.IncludedInPayroll)
			.Select(x => x.Date)
			.ToHashSet();
		var units = new Dictionary<DateOnly, decimal>();

		for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
		{
			var dateOnly = DateOnly.FromDateTime(date);
			var unit = GetBaseWorkingUnit(date.DayOfWeek, saturdayWorkPolicy);
			if (unit > 0m && includedHolidayDates.Contains(dateOnly))
			{
				unit = 0m;
			}

			units[dateOnly] = unit;
		}

		return units;
	}

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

	private static decimal GetBaseWorkingUnit(DayOfWeek dayOfWeek, SaturdayWorkPolicy saturdayWorkPolicy)
	{
		return dayOfWeek switch
		{
			DayOfWeek.Sunday => 0m,
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
}

public static class PayrollBasisDays
{
	public static PayrollBasisDaysResult Resolve(
		DayCalculationSetting? setting,
		int year,
		int month,
		decimal workingWeekdaysMinusHolidayWeekdays,
		IReadOnlyDictionary<DateOnly, decimal>? workingUnitsByDate = null)
	{
		if (setting is null)
		{
			return new PayrollBasisDaysResult(
				workingWeekdaysMinusHolidayWeekdays,
				DayCalculationType.WorkingDays,
				UsedTenantDefault: true,
				SaturdayWorkPolicy.FullDay,
				workingUnitsByDate);
		}

		var effectiveMode = setting.GetEffectiveDayCalculationTypeForMonth(year, month);
		var effectiveSaturdayPolicy = setting.GetEffectiveSaturdayWorkPolicyForMonth(year, month);

		return effectiveMode switch
		{
			DayCalculationType.WorkingDays => new PayrollBasisDaysResult(
				workingWeekdaysMinusHolidayWeekdays,
				DayCalculationType.WorkingDays,
				UsedTenantDefault: false,
				effectiveSaturdayPolicy,
				workingUnitsByDate),

			DayCalculationType.TotalMonthDays => new PayrollBasisDaysResult(
				DateTime.DaysInMonth(year, month),
				DayCalculationType.TotalMonthDays,
				UsedTenantDefault: false,
				effectiveSaturdayPolicy,
				workingUnitsByDate),

			DayCalculationType.ManualDays => new PayrollBasisDaysResult(
				setting.GetEffectiveManualDaysForMonth(year, month)
					?? throw new InvalidOperationException($"ManualDays must be set for {year}-{month:00} when mode is ManualDays."),
				DayCalculationType.ManualDays,
				UsedTenantDefault: false,
				effectiveSaturdayPolicy,
				workingUnitsByDate),

			_ => throw new ArgumentOutOfRangeException(nameof(setting), effectiveMode, "Unknown day calculation type.")
		};
	}
}

public static class PayrollProrationCalculator
{
	public static decimal CalculatePayableDays(int presentDays, int halfWorkingDays) =>
		presentDays + 0.5m * halfWorkingDays;

	public static decimal CalculateProrationFactor(decimal basisDays, decimal payableDays)
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
		IReadOnlyList<PayrollApprovedAttendanceDay> approvedDays,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate = null)
	{
		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var monthRecords = approvedDays
			.Where(a => a.Date >= startDate && a.Date <= endDate)
			.GroupBy(a => a.Date)
			.Select(g => g.Last())
			.ToList();

		var workingUnitsByDate = basis.WorkingUnitsByDate
			?? PayrollMonthCalendar.BuildWorkingUnitsByDate(
				year,
				month,
				holidays,
				basis.SaturdayWorkPolicy,
				Array.Empty<PayrollHolidayOverride>());

		if (basis.EffectiveMode == DayCalculationType.WorkingDays)
		{
			monthRecords = monthRecords.Where(a => IsWorkingDay(a.Date, workingUnitsByDate)).ToList();
		}

		var present = monthRecords.Count(a => a.Status == EmployeeAttendanceStatusType.Present);
		var half = monthRecords.Count(a => a.Status == EmployeeAttendanceStatusType.HalfDayLeave);
		var payableByDate = monthRecords.ToDictionary(
			a => a.Date,
			a =>
			{
				var maxUnits = basis.EffectiveMode == DayCalculationType.WorkingDays
					? GetWorkingUnit(a.Date, workingUnitsByDate)
					: 1m;
				return Math.Clamp(a.PayableUnits, 0m, maxUnits);
			});

		if (basis.EffectiveMode == DayCalculationType.TotalMonthDays)
		{
			var unpaidDeduction = monthRecords.Sum(a =>
			{
				var leaveUnits = GetLeaveUnits(a.Date, leaveUnitsByDate);
				return a.Status switch
				{
					EmployeeAttendanceStatusType.FullDayLeave => Math.Max(0m, 1m - leaveUnits.PaidUnits),
					EmployeeAttendanceStatusType.HalfDayLeave => Math.Max(0m, 0.5m - leaveUnits.PaidUnits),
					_ => 0m
				};
			});
			var attendanceDates = monthRecords
				.Where(a => a.Status == EmployeeAttendanceStatusType.Present)
				.Select(a => a.Date)
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
						.Where(a => a.Date == date)
						.Sum(a => a.Status switch
						{
							EmployeeAttendanceStatusType.FullDayLeave => Math.Max(0m, 1m - GetLeaveUnits(date, leaveUnitsByDate).PaidUnits),
							EmployeeAttendanceStatusType.HalfDayLeave => Math.Max(0m, 0.5m - GetLeaveUnits(date, leaveUnitsByDate).PaidUnits),
							_ => 0m
						});
					unpaidDeduction += Math.Max(0m, Math.Clamp(leaveUnits.UnpaidUnits, 0m, 1m) - existingDeduction);
				}
			}

			var totalMonthPayableDays = Math.Max(0m, basis.BasisDays - unpaidDeduction);
			return new PayrollAttendancePayableDays(present, half, totalMonthPayableDays);
		}

		var payableDays = payableByDate.Values.Sum();
		if (leaveUnitsByDate is { Count: > 0 })
		{
			foreach (var (date, leaveUnits) in leaveUnitsByDate)
			{
				if (date < startDate || date > endDate)
				{
					continue;
				}

				if (basis.EffectiveMode == DayCalculationType.WorkingDays && !IsWorkingDay(date, workingUnitsByDate))
				{
					continue;
				}

				var maxUnitsForDate = basis.EffectiveMode == DayCalculationType.WorkingDays
					? GetWorkingUnit(date, workingUnitsByDate)
					: 1m;
				var paidUnitsForDate = Math.Clamp(leaveUnits.PaidUnits, 0m, maxUnitsForDate);
				var attendanceUnits = payableByDate.GetValueOrDefault(date);
				if (paidUnitsForDate > attendanceUnits)
				{
					payableDays += paidUnitsForDate - attendanceUnits;
				}
			}
		}

		return new PayrollAttendancePayableDays(present, half, Math.Max(0m, payableDays));
	}

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

		var workingUnitsByDate = basis.WorkingUnitsByDate
			?? PayrollMonthCalendar.BuildWorkingUnitsByDate(
				year,
				month,
				holidays,
				basis.SaturdayWorkPolicy,
				Array.Empty<PayrollHolidayOverride>());
		if (basis.EffectiveMode == DayCalculationType.WorkingDays)
		{
			monthRecords = monthRecords
				.Where(a => IsWorkingDay(a.AttendanceDate, workingUnitsByDate))
				.ToList();
		}

		var present = monthRecords.Count(a => a.StatusType == EmployeeAttendanceStatusType.Present);
		var half = monthRecords.Count(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);
		var attendancePayableByDate = monthRecords.ToDictionary(
			a => a.AttendanceDate,
			a =>
			{
				var dayUnit = basis.EffectiveMode == DayCalculationType.WorkingDays
					? GetWorkingUnit(a.AttendanceDate, workingUnitsByDate)
					: 1m;

				return a.StatusType switch
				{
					EmployeeAttendanceStatusType.Present => dayUnit,
					EmployeeAttendanceStatusType.HalfDayLeave => dayUnit * 0.5m,
					_ => 0m
				};
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

				if (basis.EffectiveMode == DayCalculationType.WorkingDays && !IsWorkingDay(date, workingUnitsByDate))
				{
					continue;
				}

				var maxUnitsForDate = basis.EffectiveMode == DayCalculationType.WorkingDays
					? GetWorkingUnit(date, workingUnitsByDate)
					: 1m;
				var paidUnitsForDate = Math.Clamp(leaveUnits.PaidUnits, 0m, maxUnitsForDate);
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

	private static bool IsWorkingDay(DateOnly date, IReadOnlyDictionary<DateOnly, decimal> workingUnitsByDate) =>
		GetWorkingUnit(date, workingUnitsByDate) > 0m;

	private static decimal GetWorkingUnit(DateOnly date, IReadOnlyDictionary<DateOnly, decimal> workingUnitsByDate) =>
		workingUnitsByDate.TryGetValue(date, out var unit) ? unit : 0m;

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
		decimal basisDays,
		IReadOnlyList<EmployeePayrollComponent> components)
	{
		var payableDays = PayrollProrationCalculator.CalculatePayableDays(presentDays, halfWorkingDays);
		return Calculate(presentDays, halfWorkingDays, payableDays, basisDays, components);
	}

	public static PayrollComputationResult Calculate(
		int presentDays,
		int halfWorkingDays,
		decimal payableDays,
		decimal basisDays,
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
