using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollAttendanceCalculator
{
	public static decimal GetPayableUnitsForStatus(
		EmployeeAttendanceStatusType status,
		decimal maxUnits) => status switch
	{
		EmployeeAttendanceStatusType.Present => maxUnits,
		EmployeeAttendanceStatusType.HalfDayLeave => maxUnits * 0.5m,
		_ => 0m
	};

	public static PayrollAttendancePayableDays CalculatePayableDays(
		IReadOnlyList<PayrollApprovedAttendanceDay> approvedDays,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate = null,
		DateOnly? employmentStartDate = null)
	{
		var monthStartDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var startDate = employmentStartDate.HasValue && employmentStartDate.Value > monthStartDate
			? employmentStartDate.Value
			: monthStartDate;
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

		var payableDays = ApplyApprovedLeaveUnits(
			payableByDate.Values.Sum(),
			payableByDate,
			leaveUnitsByDate,
			startDate,
			endDate,
			basis,
			workingUnitsByDate);

		return new PayrollAttendancePayableDays(present, half, Math.Max(0m, payableDays));
	}

	public static PayrollAttendancePayableDays CalculatePayableDays(
		IReadOnlyList<EmployeeAttendance> attendanceRecords,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate = null,
		DateOnly? employmentStartDate = null)
	{
		var monthStartDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var startDate = employmentStartDate.HasValue && employmentStartDate.Value > monthStartDate
			? employmentStartDate.Value
			: monthStartDate;
		var monthRecords = attendanceRecords
			.Where(a => a.AttendanceDate >= startDate && a.AttendanceDate <= endDate)
			.GroupBy(a => a.AttendanceDate)
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

				return GetPayableUnitsForStatus(a.StatusType, dayUnit);
			});

		var payableDays = ApplyApprovedLeaveUnits(
			attendancePayableByDate.Values.Sum(),
			attendancePayableByDate,
			leaveUnitsByDate,
			startDate,
			endDate,
			basis,
			workingUnitsByDate);

		return new PayrollAttendancePayableDays(
			present,
			half,
			Math.Max(0m, payableDays));
	}

	private static decimal ApplyApprovedLeaveUnits(
		decimal payableDays,
		Dictionary<DateOnly, decimal> payableByDate,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		DateOnly startDate,
		DateOnly endDate,
		PayrollBasisDaysResult basis,
		IReadOnlyDictionary<DateOnly, decimal> workingUnitsByDate)
	{
		if (leaveUnitsByDate is not { Count: > 0 })
		{
			return payableDays;
		}

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
			var currentUnits = payableByDate.GetValueOrDefault(date);
			var resolvedUnits = ResolvePayableUnitsWithApprovedLeave(currentUnits, leaveUnits, maxUnitsForDate);
			if (resolvedUnits == currentUnits)
			{
				continue;
			}

			payableDays += resolvedUnits - currentUnits;
			payableByDate[date] = resolvedUnits;
		}

		return payableDays;
	}

	private static decimal ResolvePayableUnitsWithApprovedLeave(
		decimal currentUnits,
		PayrollLeaveUnits leaveUnits,
		decimal maxUnitsForDate)
	{
		var paidUnitsForDate = Math.Clamp(leaveUnits.PaidUnits, 0m, maxUnitsForDate);
		var unpaidUnitsForDate = Math.Clamp(leaveUnits.UnpaidUnits, 0m, maxUnitsForDate);
		var attendanceCapacityAfterUnpaidLeave = Math.Max(0m, maxUnitsForDate - unpaidUnitsForDate);
		var attendanceUnitsAfterUnpaidLeave = Math.Min(currentUnits, attendanceCapacityAfterUnpaidLeave);

		return Math.Clamp(Math.Max(attendanceUnitsAfterUnpaidLeave, paidUnitsForDate), 0m, maxUnitsForDate);
	}

	private static bool IsWorkingDay(DateOnly date, IReadOnlyDictionary<DateOnly, decimal> workingUnitsByDate) =>
		GetWorkingUnit(date, workingUnitsByDate) > 0m;

	private static decimal GetWorkingUnit(DateOnly date, IReadOnlyDictionary<DateOnly, decimal> workingUnitsByDate) =>
		workingUnitsByDate.TryGetValue(date, out var unit) ? unit : 0m;
}
