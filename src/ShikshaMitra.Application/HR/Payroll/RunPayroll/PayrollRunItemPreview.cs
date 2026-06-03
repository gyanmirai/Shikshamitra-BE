using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;

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
		DateOnly? employmentStartDate)
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
		var computation = PayrollComputation.Calculate(
			attendanceSummary.PresentDays,
			attendanceSummary.HalfWorkingDays,
			attendanceSummary.PayableDays,
			basis.BasisDays,
			allComponents);
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
}
