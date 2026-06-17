using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollChainFunctional_Tests
{
	[Fact]
	public void Component_Month_Scope_Should_Include_Temp_Rows_Only_For_Applicable_Month()
	{
		var employeeId = Guid.NewGuid();
		var components = new List<EmployeePayrollComponent>
		{
			BuildComponent(employeeId, "Basic", 30000m, isEarning: true),
			BuildComponent(employeeId, "January Bonus", 100m, isEarning: true, isTemp: true, applicableMonth: new DateTime(2026, 1, 20)),
			BuildComponent(employeeId, "February Recovery", 50m, isEarning: false, isTemp: true, applicableMonth: new DateTime(2026, 2, 1)),
			BuildComponent(employeeId, "Undated Temp", 25m, isEarning: true, isTemp: true)
		};

		var januaryComponents = PayrollComponentMonthScope.FilterForMonth(components, 2026, 1);
		var februaryComponents = PayrollComponentMonthScope.FilterForMonth(components, 2026, 2);

		januaryComponents.Select(x => x.Name).ShouldBe(["Basic", "January Bonus"]);
		februaryComponents.Select(x => x.Name).ShouldBe(["Basic", "February Recovery"]);
	}

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

	[Fact]
	public void Work_Calendar_SaturdaySunday_Should_Exclude_Saturday_And_Sunday()
	{
		var calendar = BuildWorkCalendar(AttendanceWeeklyOffPattern.SaturdaySunday);

		var units = PayrollMonthCalendar.BuildWorkingUnitsByDate(
			2026,
			6,
			holidays: [],
			SaturdayWorkPolicy.FullDay,
			holidayOverrides: [],
			calendar);

		units[new DateOnly(2026, 6, 6)].ShouldBe(0m);
		units[new DateOnly(2026, 6, 7)].ShouldBe(0m);
		units.Values.Sum().ShouldBe(22m);
	}

	[Theory]
	[InlineData(SaturdayWorkPolicy.FullDay, 1, 26)]
	[InlineData(SaturdayWorkPolicy.HalfDay, 0.5, 24)]
	[InlineData(SaturdayWorkPolicy.Off, 0, 22)]
	public void Work_Calendar_Sunday_Should_Allow_Saturday_And_Apply_Saturday_Policy(
		SaturdayWorkPolicy saturdayWorkPolicy,
		double expectedSaturdayUnit,
		double expectedMonthUnits)
	{
		var calendar = BuildWorkCalendar(AttendanceWeeklyOffPattern.Sunday);

		var units = PayrollMonthCalendar.BuildWorkingUnitsByDate(
			2026,
			6,
			holidays: [],
			saturdayWorkPolicy,
			holidayOverrides: [],
			calendar);

		units[new DateOnly(2026, 6, 6)].ShouldBe(Convert.ToDecimal(expectedSaturdayUnit));
		units[new DateOnly(2026, 6, 7)].ShouldBe(0m);
		units.Values.Sum().ShouldBe(Convert.ToDecimal(expectedMonthUnits));
	}

	[Fact]
	public void Work_Calendar_Custom_Should_Respect_Custom_Weekly_Offs_Before_Saturday_Policy()
	{
		var calendar = BuildWorkCalendar(AttendanceWeeklyOffPattern.Custom, new List<int> { 6 });

		var units = PayrollMonthCalendar.BuildWorkingUnitsByDate(
			2026,
			6,
			holidays: [],
			SaturdayWorkPolicy.FullDay,
			holidayOverrides: [],
			calendar);

		units[new DateOnly(2026, 6, 6)].ShouldBe(0m);
		units[new DateOnly(2026, 6, 7)].ShouldBe(1m);
		units.Values.Sum().ShouldBe(26m);
	}

	[Fact]
	public void Work_Calendar_Holiday_Should_Reduce_Working_Days_Basis()
	{
		var calendar = BuildWorkCalendar(AttendanceWeeklyOffPattern.SaturdaySunday);
		var holiday = BuildHoliday(new DateTime(2026, 6, 3));

		var units = PayrollMonthCalendar.BuildWorkingUnitsByDate(
			2026,
			6,
			[holiday],
			SaturdayWorkPolicy.FullDay,
			holidayOverrides: [],
			calendar);

		units[new DateOnly(2026, 6, 3)].ShouldBe(0m);
		units.Values.Sum().ShouldBe(21m);
	}

	[Fact]
	public void Unpaid_Payroll_Holiday_Override_Should_Reduce_Working_Days_Basis()
	{
		var calendar = BuildWorkCalendar(AttendanceWeeklyOffPattern.SaturdaySunday);
		var holiday = BuildHoliday(new DateTime(2026, 6, 3));
		var overrides = new List<PayrollHolidayOverride>
		{
			new(new DateTime(2026, 6, 3), "Unpaid Override", IncludedInPayroll: false, CalendarHolidayId: holiday.Id)
		};

		var units = PayrollMonthCalendar.BuildWorkingUnitsByDate(
			2026,
			6,
			[holiday],
			SaturdayWorkPolicy.FullDay,
			overrides,
			calendar);

		units[new DateOnly(2026, 6, 3)].ShouldBe(0m);
		units.Values.Sum().ShouldBe(21m);
	}

	[Fact]
	public void Attendance_On_Work_Calendar_Holiday_Should_Not_Contribute_Payable_Units()
	{
		var workingUnits = BuildWeekdayUnits(2026, 6);
		workingUnits[new DateOnly(2026, 6, 3)] = 0m;
		var basis = new PayrollBasisDaysResult(
			BasisDays: 21m,
			EffectiveMode: DayCalculationType.WorkingDays,
			UsedTenantDefault: false,
			SaturdayWorkPolicy.FullDay,
			workingUnits);
		var attendance = new List<EmployeeAttendance>
		{
			BuildAttendance(new DateOnly(2026, 6, 3), EmployeeAttendanceStatusType.Present)
		};

		var result = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			6,
			holidays: []);

		result.PresentDays.ShouldBe(0);
		result.PayableDays.ShouldBe(0m);
	}

	[Fact]
	public void Full_Month_Holiday_Should_Return_Zero_Basis_And_Zero_Proration()
	{
		var calendar = BuildWorkCalendar(AttendanceWeeklyOffPattern.SaturdaySunday);
		var holiday = BuildHoliday(new DateTime(2026, 6, 1), new DateTime(2026, 6, 30));
		var workingUnits = PayrollMonthCalendar.BuildWorkingUnitsByDate(
			2026,
			6,
			[holiday],
			SaturdayWorkPolicy.FullDay,
			holidayOverrides: [],
			calendar);
		var basis = new PayrollBasisDaysResult(
			BasisDays: workingUnits.Values.Sum(),
			EffectiveMode: DayCalculationType.WorkingDays,
			UsedTenantDefault: false,
			SaturdayWorkPolicy.FullDay,
			workingUnits);
		var attendance = new List<EmployeeAttendance>
		{
			BuildAttendance(new DateOnly(2026, 6, 3), EmployeeAttendanceStatusType.Present)
		};

		var result = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			6,
			holidays: []);

		basis.BasisDays.ShouldBe(0m);
		result.PresentDays.ShouldBe(0);
		result.PayableDays.ShouldBe(0m);
		PayrollProrationCalculator.CalculateProrationFactor(basis.BasisDays, result.PayableDays).ShouldBe(0m);
	}

	[Fact]
	public void Attendance_Before_Joining_Date_Should_Not_Count_For_Payroll()
	{
		var basis = new PayrollBasisDaysResult(
			BasisDays: 30m,
			EffectiveMode: DayCalculationType.TotalMonthDays,
			UsedTenantDefault: false,
			SaturdayWorkPolicy.FullDay,
			BuildWeekdayUnits(2026, 4));
		var attendance = new List<EmployeeAttendance>
		{
			BuildAttendance(new DateOnly(2026, 4, 24), EmployeeAttendanceStatusType.Present),
			BuildAttendance(new DateOnly(2026, 4, 25), EmployeeAttendanceStatusType.Present),
			BuildAttendance(new DateOnly(2026, 4, 30), EmployeeAttendanceStatusType.Present)
		};

		var result = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			holidays: [],
			leaveUnitsByDate: null,
			employmentStartDate: new DateOnly(2026, 4, 25));

		result.PresentDays.ShouldBe(2);
		result.PayableDays.ShouldBe(2m);
	}

	[Fact]
	public void Approved_Paid_Leave_Before_Joining_Date_Should_Not_Restore_Payable_Units()
	{
		var basis = BuildWorkingDaysBasis(2026, 4);
		var attendance = new List<EmployeeAttendance>();
		var leaveUnits = new Dictionary<DateOnly, PayrollLeaveUnits>
		{
			[new DateOnly(2026, 4, 24)] = new(PaidUnits: 1m, UnpaidUnits: 0m),
			[new DateOnly(2026, 4, 27)] = new(PaidUnits: 1m, UnpaidUnits: 0m)
		};

		var result = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			holidays: [],
			leaveUnits,
			employmentStartDate: new DateOnly(2026, 4, 25));

		result.PayableDays.ShouldBe(1m);
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

	private static WorkCalendarPolicy BuildWorkCalendar(
		AttendanceWeeklyOffPattern weeklyOffPattern,
		IReadOnlyList<int>? customWeeklyOffs = null) => new(
		Guid.NewGuid(),
		tenantId: null,
		Guid.NewGuid(),
		"Payroll Calendar",
		HrPolicyStatus.Active,
		new DateTime(2026, 4, 1),
		new DateTime(2027, 3, 31),
		weeklyOffPattern,
		customWeeklyOffs,
		WorkCalendarHolidaySyncMode.Snapshot);

	private static Holiday BuildHoliday(DateTime date, DateTime? toDate = null) =>
		Holiday.Create(
			tenantId: null,
			Guid.NewGuid(),
			"Payroll Holiday",
			Guid.NewGuid(),
			DateOnly.FromDateTime(date.Date),
			DateOnly.FromDateTime((toDate ?? date).Date));

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
		bool isTemp = false,
		DateTime? applicableMonth = null) => new(
		Guid.NewGuid(),
		employeeId,
		name,
		componentId: null,
		description: null,
		amount,
		isFixed: true,
		isTemp,
		applicableMonth,
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
