using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal sealed record PayrollRunItemPreview(
	PayrollAttendancePayableDays AttendanceSummary,
	PayrollComputationResult Computation,
	decimal? NetPayDelta,
	decimal? NetPayDeltaPercent,
	bool HasAttendanceOrPaidLeave)
{
	public static PayrollRunItemPreview Prepare(
		List<EmployeeAttendance> attendanceRecords,
		List<EmployeePayrollComponent> components,
		List<PayrollAdjustment> adjustments,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		decimal? previousMonthNetPay,
		DateOnly? employmentStartDate,
		ShiftManagement? shift = null,
		AttendancePolicy? attendancePolicy = null,
		PayrollStatutoryRuleSet? statutoryRules = null)
	{
		var attendanceSummary = PayrollAttendanceCalculator.CalculatePayableDays(
			attendanceRecords,
			basis,
			year,
			month,
			holidays,
			leaveUnitsByDate,
			employmentStartDate);
		var allComponents = components
			.Concat(adjustments.Select(PayrollRunProjection.ToPayrollComponent))
			.ToList();
		var overtimeComponent = BuildOvertimeComponent(
			attendanceRecords,
			allComponents,
			basis,
			year,
			month,
			employmentStartDate,
			shift,
			attendancePolicy);
		if (overtimeComponent != null)
		{
			allComponents.Add(overtimeComponent);
		}

		var computation = PayrollComputation.Calculate(
			attendanceSummary.PresentDays,
			attendanceSummary.HalfWorkingDays,
			attendanceSummary.PayableDays,
			basis.BasisDays,
			allComponents,
			statutoryRules);
		var netPayDelta = previousMonthNetPay.HasValue
			? computation.NetPay - previousMonthNetPay.Value
			: (decimal?) null;
		var netPayDeltaPercent = previousMonthNetPay.HasValue && previousMonthNetPay.Value != 0
			? netPayDelta / previousMonthNetPay.Value * 100m
			: (decimal?) null;
		var hasAttendanceOrPaidLeave = PayrollIssueCodeBuilder.HasAttendanceOrPaidLeave(
			attendanceRecords,
			leaveUnitsByDate,
			employmentStartDate,
			year,
			month);

		return new PayrollRunItemPreview(
			attendanceSummary,
			computation,
			netPayDelta,
			netPayDeltaPercent,
			hasAttendanceOrPaidLeave);
	}

	private static EmployeePayrollComponent? BuildOvertimeComponent(
		IReadOnlyList<EmployeeAttendance> attendanceRecords,
		IReadOnlyList<EmployeePayrollComponent> components,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		DateOnly? employmentStartDate,
		ShiftManagement? shift,
		AttendancePolicy? attendancePolicy)
	{
		if (shift == null ||
		    attendancePolicy?.OvertimeEnabled != true ||
		    attendancePolicy.OvertimeRate == AttendanceOvertimeRate.Flat ||
		    !attendancePolicy.MinOvertimeMinutes.HasValue ||
		    basis.BasisDays <= 0m)
		{
			return null;
		}

		var shiftMinutes = (decimal)(shift.EndTime.ToTimeSpan() - shift.StartTime.ToTimeSpan()).TotalMinutes;
		if (shiftMinutes <= 0m)
		{
			return null;
		}

		var overtimeMinutes = CalculateOvertimeMinutes(
			attendanceRecords,
			year,
			month,
			employmentStartDate,
			shift.EndTime);
		if (overtimeMinutes < attendancePolicy.MinOvertimeMinutes.Value)
		{
			return null;
		}

		var multiplier = attendancePolicy.OvertimeRate switch
		{
			AttendanceOvertimeRate.OnePointFiveX => 1.5m,
			AttendanceOvertimeRate.TwoX => 2m,
			_ => 0m
		};
		if (multiplier <= 0m)
		{
			return null;
		}

		var fullGrossSalary = components.Where(c => c.IsEarning).Sum(c => c.Amount);
		if (fullGrossSalary <= 0m)
		{
			return null;
		}

		var scheduledHours = shiftMinutes / 60m;
		var overtimeHours = overtimeMinutes / 60m;
		var dailyRate = fullGrossSalary / basis.BasisDays;
		var amount = Math.Round(dailyRate / scheduledHours * overtimeHours * multiplier, 2, MidpointRounding.AwayFromZero);
		if (amount <= 0m)
		{
			return null;
		}

		return new EmployeePayrollComponent(
			Guid.NewGuid(),
			components.FirstOrDefault()?.EmployeeId ?? Guid.Empty,
			"Overtime Pay",
			componentId: null,
			description: $"{overtimeMinutes:0.##} overtime minutes",
			amount,
			isFixed: true,
			isTemp: true,
			applicableMonth: new DateTime(year, month, 1),
			SalaryComponentType.Fixed,
			SalaryComponentDependency.None,
			isEarning: true,
			usesMasterValue: false);
	}

	private static decimal CalculateOvertimeMinutes(
		IReadOnlyList<EmployeeAttendance> attendanceRecords,
		int year,
		int month,
		DateOnly? employmentStartDate,
		TimeOnly shiftEndTime)
	{
		var monthStartDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var startDate = employmentStartDate.HasValue && employmentStartDate.Value > monthStartDate
			? employmentStartDate.Value
			: monthStartDate;

		return attendanceRecords
			.Where(a =>
				a.AttendanceDate >= startDate &&
				a.AttendanceDate <= endDate &&
				a.StatusType == EmployeeAttendanceStatusType.Present &&
				a.TimeIn.HasValue &&
				a.TimeOut.HasValue)
			.GroupBy(a => a.AttendanceDate)
			.Select(g => g.Last())
			.Sum(a =>
			{
				var timeOut = a.TimeOut!.Value;
				var utcTimeOut = timeOut.UtcDateTime;
				var utcTimeOutDate = DateOnly.FromDateTime(utcTimeOut);
				var dayOffset = utcTimeOutDate.DayNumber - a.AttendanceDate.DayNumber;
				var minutesAfterShiftEnd =
					dayOffset * 24m * 60m +
					(decimal)(utcTimeOut.TimeOfDay - shiftEndTime.ToTimeSpan()).TotalMinutes;
				return Math.Max(0m, minutesAfterShiftEnd);
			});
	}
}
