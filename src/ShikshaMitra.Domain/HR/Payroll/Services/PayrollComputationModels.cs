using System;
using System.Collections.Generic;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;

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
