using System;
using System.Collections.Generic;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollChainFunctional_Tests
{
	[Fact]
	public void Full_Present_Attendance_Should_Pay_Full_Salary()
	{
		var result = PayrollComputation.Calculate(
			presentDays: 22,
			halfWorkingDays: 0,
			payableDays: 22m,
			basisDays: 22m,
			components: BuildSalaryComponents());

		result.PayableDays.ShouldBe(22m);
		result.ProrationFactor.ShouldBe(1m);
		result.BasicSalary.ShouldBe(30000m);
		result.FullGrossSalary.ShouldBe(52000m);
		result.GrossSalary.ShouldBe(52000m);
		result.TotalDeduction.ShouldBe(2600m);
		result.NetPay.ShouldBe(49400m);
	}

	[Fact]
	public void Half_Days_Should_Reduce_Payable_Days_And_Prorate_Non_Temp_Lines()
	{
		var result = PayrollComputation.Calculate(
			presentDays: 20,
			halfWorkingDays: 2,
			payableDays: 21m,
			basisDays: 22m,
			components: BuildSalaryComponents());

		result.PayableDays.ShouldBe(21m);
		result.ProrationFactor.ShouldBe(21m / 22m);
		result.BasicSalary.ShouldBe(28636.36m);
		result.GrossSalary.ShouldBe(49659.09m);
		result.TotalDeduction.ShouldBe(2509.09m);
		result.NetPay.ShouldBe(47150.00m);
		result.Earnings.ShouldContain(x => x.Source.Name == "One Time Bonus" && x.Amount == 500m);
	}

	[Fact]
	public void Approved_Paid_Leave_Should_Restore_Payable_Units_For_Working_Day_Mode()
	{
		var basis = BuildWorkingDaysBasis(2026, 4);
		var attendance = new List<PayrollApprovedAttendanceDay>
		{
			new(new DateOnly(2026, 4, 1), EmployeeAttendanceStatusType.Present, 1m),
			new(new DateOnly(2026, 4, 2), EmployeeAttendanceStatusType.FullDayLeave, 0m)
		};
		var leaveUnits = new Dictionary<DateOnly, PayrollLeaveUnits>
		{
			[new DateOnly(2026, 4, 2)] = new(PaidUnits: 1m, UnpaidUnits: 0m)
		};

		var result = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			holidays: [],
			leaveUnits);

		result.PresentDays.ShouldBe(1);
		result.PayableDays.ShouldBe(2m);
	}

	[Fact]
	public void Approved_Lop_Should_Not_Restore_Payable_Units()
	{
		var basis = BuildWorkingDaysBasis(2026, 4);
		var attendance = new List<PayrollApprovedAttendanceDay>
		{
			new(new DateOnly(2026, 4, 1), EmployeeAttendanceStatusType.Present, 1m),
			new(new DateOnly(2026, 4, 2), EmployeeAttendanceStatusType.FullDayLeave, 0m)
		};
		var leaveUnits = new Dictionary<DateOnly, PayrollLeaveUnits>
		{
			[new DateOnly(2026, 4, 2)] = new(PaidUnits: 0m, UnpaidUnits: 1m)
		};

		var result = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			holidays: [],
			leaveUnits);

		result.PresentDays.ShouldBe(1);
		result.PayableDays.ShouldBe(1m);
	}

	[Theory]
	[InlineData(DayCalculationType.WorkingDays, 22)]
	[InlineData(DayCalculationType.TotalMonthDays, 30)]
	[InlineData(DayCalculationType.ManualDays, 26)]
	public void Denominator_Mode_Should_Control_Proration_Basis(DayCalculationType mode, decimal expectedBasisDays)
	{
		var basis = new PayrollBasisDaysResult(
			expectedBasisDays,
			mode,
			UsedTenantDefault: false,
			SaturdayWorkPolicy.FullDay,
			BuildWeekdayUnits(2026, 4));

		var factor = PayrollProrationCalculator.CalculateProrationFactor(basis.BasisDays, payableDays: 20m);

		factor.ShouldBe(Math.Min(1m, 20m / expectedBasisDays));
		PayrollDayCalculationApiLabels.ToApiString(mode).ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Total_Month_Days_Should_Not_Pay_Missing_Attendance_By_Default()
	{
		var basis = new PayrollBasisDaysResult(
			BasisDays: 30m,
			EffectiveMode: DayCalculationType.TotalMonthDays,
			UsedTenantDefault: false,
			SaturdayWorkPolicy.FullDay,
			BuildWeekdayUnits(2026, 4));

		var result = PayrollAttendanceCalculator.CalculatePayableDays(
			Array.Empty<EmployeeAttendance>(),
			basis,
			2026,
			4,
			holidays: []);

		result.PresentDays.ShouldBe(0);
		result.PayableDays.ShouldBe(0m);
	}

	[Fact]
	public void Total_Month_Days_Should_Use_Final_Attendance_Status()
	{
		var basis = new PayrollBasisDaysResult(
			BasisDays: 30m,
			EffectiveMode: DayCalculationType.TotalMonthDays,
			UsedTenantDefault: false,
			SaturdayWorkPolicy.FullDay,
			BuildWeekdayUnits(2026, 4));
		var attendance = new List<EmployeeAttendance>
		{
			BuildAttendance(new DateOnly(2026, 4, 1), EmployeeAttendanceStatusType.Present),
			BuildAttendance(new DateOnly(2026, 4, 2), EmployeeAttendanceStatusType.HalfDayLeave),
			BuildAttendance(new DateOnly(2026, 4, 3), EmployeeAttendanceStatusType.FullDayLeave)
		};

		var result = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			holidays: []);

		result.PresentDays.ShouldBe(1);
		result.HalfWorkingDays.ShouldBe(1);
		result.PayableDays.ShouldBe(1.5m);
	}

	private static PayrollBasisDaysResult BuildWorkingDaysBasis(int year, int month)
	{
		var workingUnits = BuildWeekdayUnits(year, month);
		return new PayrollBasisDaysResult(
			BasisDays: 22m,
			EffectiveMode: DayCalculationType.WorkingDays,
			UsedTenantDefault: false,
			SaturdayWorkPolicy.FullDay,
			workingUnits);
	}

	private static Dictionary<DateOnly, decimal> BuildWeekdayUnits(int year, int month)
	{
		var units = new Dictionary<DateOnly, decimal>();
		for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
		{
			var date = new DateOnly(year, month, day);
			var dayOfWeek = date.DayOfWeek;
			units[date] = dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0m : 1m;
		}

		return units;
	}

	private static List<EmployeePayrollComponent> BuildSalaryComponents()
	{
		var employeeId = Guid.NewGuid();
		return new List<EmployeePayrollComponent>
		{
			BuildComponent(employeeId, "Basic", 30000m, isEarning: true),
			BuildComponent(employeeId, "HRA", 12000m, isEarning: true),
			BuildComponent(employeeId, "Special Allowance", 9500m, isEarning: true),
			BuildComponent(employeeId, "One Time Bonus", 500m, isEarning: true, isTemp: true),
			BuildComponent(employeeId, "PF", 1800m, isEarning: false),
			BuildComponent(employeeId, "Professional Tax", 200m, isEarning: false),
			BuildComponent(employeeId, "One Time Recovery", 600m, isEarning: false, isTemp: true)
		};
	}

	private static EmployeePayrollComponent BuildComponent(
		Guid employeeId,
		string name,
		decimal amount,
		bool isEarning,
		bool isTemp = false) => new(
		Guid.NewGuid(),
		employeeId,
		name,
		componentId: null,
		description: null,
		amount,
		isFixed: true,
		isTemp,
		applicableMonth: null,
		SalaryComponentType.Fixed,
		SalaryComponentDependency.None,
		isEarning);

	private static EmployeeAttendance BuildAttendance(
		DateOnly date,
		EmployeeAttendanceStatusType status) => new()
	{
		EmployeeId = Guid.NewGuid(),
		AttendanceDate = date,
		StatusType = status
	};
}
