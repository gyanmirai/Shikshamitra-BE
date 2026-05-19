using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollComputationCoreTests
{
	[Fact]
	public void Resolve_Uses_WorkingDays_When_Setting_Null()
	{
		var r = PayrollBasisDays.Resolve(null, 2026, 4, 21);
		Assert.Equal(21, r.BasisDays);
		Assert.Equal(DayCalculationType.WorkingDays, r.EffectiveMode);
		Assert.True(r.UsedTenantDefault);
	}

	[Fact]
	public void Resolve_TotalMonthDays_Ignores_WorkingCount_Param()
	{
		var setting = new DayCalculationSetting(Guid.NewGuid(), Guid.NewGuid(), DayCalculationType.TotalMonthDays, null);
		var r = PayrollBasisDays.Resolve(setting, 2026, 4, 999);
		Assert.Equal(30, r.BasisDays);
		Assert.False(r.UsedTenantDefault);
	}

	[Fact]
	public void Resolve_ManualDays_Uses_Configured_Count()
	{
		var setting = new DayCalculationSetting(Guid.NewGuid(), Guid.NewGuid(), DayCalculationType.ManualDays, 26);
		var r = PayrollBasisDays.Resolve(setting, 2026, 4, 22);
		Assert.Equal(26, r.BasisDays);
		Assert.Equal(DayCalculationType.ManualDays, r.EffectiveMode);
	}

	[Fact]
	public void Resolve_ManualDays_Uses_Month_Specific_Count()
	{
		var setting = new DayCalculationSetting(
			Guid.NewGuid(),
			Guid.NewGuid(),
			DayCalculationType.ManualDays,
			manualDays: null,
			academicYearId: Guid.NewGuid(),
			manualMonthDays: new[]
			{
				new DayCalculationManualMonth(2026, 4, 24),
				new DayCalculationManualMonth(2026, 5, 25)
			});

		var april = PayrollBasisDays.Resolve(setting, 2026, 4, 22);
		var may = PayrollBasisDays.Resolve(setting, 2026, 5, 22);

		Assert.Equal(24, april.BasisDays);
		Assert.Equal(25, may.BasisDays);
	}

	[Fact]
	public void ProrationFactor_Capped_At_One()
	{
		var f = PayrollProrationCalculator.CalculateProrationFactor(20, 25m);
		Assert.Equal(1m, f);
	}

	[Fact]
	public void ProrationFactor_Uses_Payable_Over_Basis()
	{
		var f = PayrollProrationCalculator.CalculateProrationFactor(20, 10m);
		Assert.Equal(0.5m, f);
	}

	[Fact]
	public void ProrateAmount_Skips_Temp()
	{
		var a = PayrollProrationCalculator.ProrateAmount(1000m, isTemp: true, 0.5m);
		Assert.Equal(1000m, a);
	}

	[Fact]
	public void MonthCalendar_Subtracts_Weekday_Holiday()
	{
		var ay = Guid.NewGuid();
		var typeId = Guid.NewGuid();
		var h = Holiday.Create(null, ay, "Test", typeId, new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero));
		var n = PayrollMonthCalendar.CountWorkingWeekdaysExcludingHolidayWeekdays(2026, 4, new[] { h });
		Assert.Equal(25, n);
	}

	[Fact]
	public void MonthCalendar_Includes_Saturdays_And_Does_Not_Double_Deduct_Sunday_Holiday()
	{
		var ay = Guid.NewGuid();
		var typeId = Guid.NewGuid();
		var sundayHoliday = Holiday.Create(
			null,
			ay,
			"Sunday",
			typeId,
			new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero));

		var n = PayrollMonthCalendar.CountWorkingWeekdaysExcludingHolidayWeekdays(2026, 4, new[] { sundayHoliday });

		Assert.Equal(26, n);
	}

	[Fact]
	public void MonthCalendar_Deducts_Only_Overlapping_NonSunday_Holiday_Dates()
	{
		var ay = Guid.NewGuid();
		var typeId = Guid.NewGuid();
		var holiday = Holiday.Create(
			null,
			ay,
			"Month edge",
			typeId,
			new DateTimeOffset(2026, 3, 30, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero));

		var n = PayrollMonthCalendar.CountWorkingWeekdaysExcludingHolidayWeekdays(2026, 4, new[] { holiday });

		Assert.Equal(24, n);
	}

	[Fact]
	public void MonthCalendar_Applies_Saturday_Half_And_Off_Policies()
	{
		var full = PayrollMonthCalendar.CountWorkingDayUnits(
			2026,
			4,
			Array.Empty<Holiday>(),
			SaturdayWorkPolicy.FullDay,
			Array.Empty<PayrollHolidayOverride>());
		var half = PayrollMonthCalendar.CountWorkingDayUnits(
			2026,
			4,
			Array.Empty<Holiday>(),
			SaturdayWorkPolicy.HalfDay,
			Array.Empty<PayrollHolidayOverride>());
		var off = PayrollMonthCalendar.CountWorkingDayUnits(
			2026,
			4,
			Array.Empty<Holiday>(),
			SaturdayWorkPolicy.Off,
			Array.Empty<PayrollHolidayOverride>());

		Assert.Equal(26m, full);
		Assert.Equal(24m, half);
		Assert.Equal(22m, off);
	}

	[Fact]
	public void MonthCalendar_Excluding_Calendar_Holiday_Restores_Working_Unit()
	{
		var holiday = Holiday.Create(
			null,
			Guid.NewGuid(),
			"Operating day",
			Guid.NewGuid(),
			new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero));
		var overrides = new[]
		{
			new PayrollHolidayOverride(new DateTime(2026, 4, 8), holiday.HolidayName, false, holiday.Id)
		};

		var n = PayrollMonthCalendar.CountWorkingDayUnits(2026, 4, new[] { holiday }, SaturdayWorkPolicy.FullDay, overrides);

		Assert.Equal(26m, n);
	}

	[Fact]
	public void MonthCalendar_Payroll_Only_Holiday_Reduces_Future_Basis()
	{
		var overrides = new[]
		{
			new PayrollHolidayOverride(new DateTime(2026, 4, 6), "Payroll only", true)
		};

		var n = PayrollMonthCalendar.CountWorkingDayUnits(2026, 4, Array.Empty<Holiday>(), SaturdayWorkPolicy.FullDay, overrides);

		Assert.Equal(25m, n);
	}

	[Fact]
	public void MonthCalendar_Saturday_Holiday_Removes_Half_Unit_When_Saturday_Is_Half_Day()
	{
		var holiday = Holiday.Create(
			null,
			Guid.NewGuid(),
			"Saturday holiday",
			Guid.NewGuid(),
			new DateTimeOffset(2026, 4, 4, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 4, 4, 0, 0, 0, TimeSpan.Zero));

		var n = PayrollMonthCalendar.CountWorkingDayUnits(2026, 4, new[] { holiday }, SaturdayWorkPolicy.HalfDay, Array.Empty<PayrollHolidayOverride>());

		Assert.Equal(23.5m, n);
	}

	[Fact]
	public void WorkingDays_Ignores_Sunday_And_Holiday_Attendance_For_Payable_Days()
	{
		var holiday = Holiday.Create(
			null,
			Guid.NewGuid(),
			"Holiday",
			Guid.NewGuid(),
			new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero));
		var basis = new PayrollBasisDaysResult(25, DayCalculationType.WorkingDays, false);
		var attendance = new List<EmployeeAttendance>
		{
			Attendance(2026, 4, 4, EmployeeAttendanceStatusType.Present), // Saturday counts
			Attendance(2026, 4, 5, EmployeeAttendanceStatusType.Present), // Sunday ignored
			Attendance(2026, 4, 8, EmployeeAttendanceStatusType.Present), // Holiday ignored
			Attendance(2026, 4, 9, EmployeeAttendanceStatusType.HalfDayLeave),
			Attendance(2026, 4, 10, EmployeeAttendanceStatusType.FullDayLeave)
		};

		var summary = PayrollAttendanceCalculator.CalculatePayableDays(attendance, basis, 2026, 4, new[] { holiday });

		Assert.Equal(1, summary.PresentDays);
		Assert.Equal(1, summary.HalfWorkingDays);
		Assert.Equal(1.5m, summary.PayableDays);
	}

	[Fact]
	public void WorkingDays_Saturday_Half_Day_Present_Counts_As_Half_Payable_Day()
	{
		var workingUnits = PayrollMonthCalendar.BuildWorkingUnitsByDate(
			2026,
			4,
			Array.Empty<Holiday>(),
			SaturdayWorkPolicy.HalfDay,
			Array.Empty<PayrollHolidayOverride>());
		var basis = new PayrollBasisDaysResult(workingUnits.Values.Sum(), DayCalculationType.WorkingDays, false, SaturdayWorkPolicy.HalfDay, workingUnits);
		var attendance = new List<EmployeeAttendance>
		{
			Attendance(2026, 4, 4, EmployeeAttendanceStatusType.Present),
			Attendance(2026, 4, 11, EmployeeAttendanceStatusType.HalfDayLeave)
		};

		var summary = PayrollAttendanceCalculator.CalculatePayableDays(attendance, basis, 2026, 4, Array.Empty<Holiday>());

		Assert.Equal(1, summary.PresentDays);
		Assert.Equal(1, summary.HalfWorkingDays);
		Assert.Equal(0.75m, summary.PayableDays);
	}

	[Fact]
	public void WorkingDays_Adds_Approved_Paid_Leave_On_Working_Days()
	{
		var holiday = Holiday.Create(
			null,
			Guid.NewGuid(),
			"Holiday",
			Guid.NewGuid(),
			new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero));
		var basis = new PayrollBasisDaysResult(25, DayCalculationType.WorkingDays, false);
		var attendance = new List<EmployeeAttendance>
		{
			Attendance(2026, 4, 6, EmployeeAttendanceStatusType.FullDayLeave),
			Attendance(2026, 4, 7, EmployeeAttendanceStatusType.HalfDayLeave)
		};
		var paidLeave = new Dictionary<DateOnly, PayrollLeaveUnits>
		{
			[new(2026, 4, 6)] = new(1m, 0m),
			[new(2026, 4, 7)] = new(0.5m, 0m),
			[new(2026, 4, 8)] = new(1m, 0m), // holiday ignored
			[new(2026, 4, 12)] = new(1m, 0m) // Sunday ignored
		};

		var summary = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			new[] { holiday },
			paidLeave);

		Assert.Equal(0, summary.PresentDays);
		Assert.Equal(1, summary.HalfWorkingDays);
		Assert.Equal(1.5m, summary.PayableDays);
	}

	[Fact]
	public void WorkingDays_Unpaid_Leave_Does_Not_Add_Payable_Days()
	{
		var basis = new PayrollBasisDaysResult(26, DayCalculationType.WorkingDays, false);
		var leave = new Dictionary<DateOnly, PayrollLeaveUnits>
		{
			[new(2026, 4, 6)] = new(0m, 1m)
		};

		var summary = PayrollAttendanceCalculator.CalculatePayableDays(
			new List<EmployeeAttendance>(),
			basis,
			2026,
			4,
			Array.Empty<Holiday>(),
			leave);

		Assert.Equal(0m, summary.PayableDays);
	}

	[Fact]
	public void TotalMonthDays_Pays_NonMarked_Days_And_Deducts_Explicit_Absence()
	{
		var basis = new PayrollBasisDaysResult(30, DayCalculationType.TotalMonthDays, false);
		var attendance = new List<EmployeeAttendance>
		{
			Attendance(2026, 4, 6, EmployeeAttendanceStatusType.FullDayLeave),
			Attendance(2026, 4, 7, EmployeeAttendanceStatusType.HalfDayLeave),
			Attendance(2026, 4, 8, EmployeeAttendanceStatusType.Present)
		};

		var summary = PayrollAttendanceCalculator.CalculatePayableDays(attendance, basis, 2026, 4, Array.Empty<Holiday>());

		Assert.Equal(1, summary.PresentDays);
		Assert.Equal(1, summary.HalfWorkingDays);
		Assert.Equal(28.5m, summary.PayableDays);
	}

	[Fact]
	public void TotalMonthDays_Paid_Leave_Neutralizes_Explicit_Absence_Deduction()
	{
		var basis = new PayrollBasisDaysResult(30, DayCalculationType.TotalMonthDays, false);
		var attendance = new List<EmployeeAttendance>
		{
			Attendance(2026, 4, 6, EmployeeAttendanceStatusType.FullDayLeave),
			Attendance(2026, 4, 7, EmployeeAttendanceStatusType.HalfDayLeave),
			Attendance(2026, 4, 8, EmployeeAttendanceStatusType.FullDayLeave)
		};
		var paidLeave = new Dictionary<DateOnly, PayrollLeaveUnits>
		{
			[new(2026, 4, 6)] = new(1m, 0m),
			[new(2026, 4, 7)] = new(0.5m, 0m)
		};

		var summary = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			Array.Empty<Holiday>(),
			paidLeave);

		Assert.Equal(0, summary.PresentDays);
		Assert.Equal(1, summary.HalfWorkingDays);
		Assert.Equal(29m, summary.PayableDays);
	}

	[Fact]
	public void Approved_TotalMonthDays_Mirrors_Raw_Attendance_Deduction_Semantics()
	{
		var basis = new PayrollBasisDaysResult(30, DayCalculationType.TotalMonthDays, false);
		var approved = new List<PayrollApprovedAttendanceDay>
		{
			new(new DateOnly(2026, 4, 6), EmployeeAttendanceStatusType.Present, 1m),
			new(new DateOnly(2026, 4, 7), EmployeeAttendanceStatusType.FullDayLeave, 0m),
			new(new DateOnly(2026, 4, 8), EmployeeAttendanceStatusType.HalfDayLeave, 0.5m)
		};

		var summary = PayrollAttendanceCalculator.CalculatePayableDays(
			approved,
			basis,
			2026,
			4,
			Array.Empty<Holiday>());

		Assert.Equal(1, summary.PresentDays);
		Assert.Equal(1, summary.HalfWorkingDays);
		Assert.Equal(28.5m, summary.PayableDays);
	}

	[Fact]
	public void TotalMonthDays_Unpaid_Leave_Deducts_Without_Attendance_Row()
	{
		var basis = new PayrollBasisDaysResult(30, DayCalculationType.TotalMonthDays, false);
		var leave = new Dictionary<DateOnly, PayrollLeaveUnits>
		{
			[new(2026, 4, 6)] = new(0m, 1m),
			[new(2026, 4, 7)] = new(0m, 0.5m)
		};

		var summary = PayrollAttendanceCalculator.CalculatePayableDays(
			new List<EmployeeAttendance>(),
			basis,
			2026,
			4,
			Array.Empty<Holiday>(),
			leave);

		Assert.Equal(28.5m, summary.PayableDays);
	}

	[Fact]
	public void Shared_Calculation_Keeps_RunPayroll_And_SalarySlip_Paid_Leave_Payable_Days_Consistent()
	{
		var basis = new PayrollBasisDaysResult(26, DayCalculationType.WorkingDays, false);
		var attendance = new List<EmployeeAttendance>
		{
			Attendance(2026, 4, 6, EmployeeAttendanceStatusType.Present),
			Attendance(2026, 4, 7, EmployeeAttendanceStatusType.FullDayLeave),
			Attendance(2026, 4, 8, EmployeeAttendanceStatusType.HalfDayLeave)
		};
		var paidLeave = new Dictionary<DateOnly, PayrollLeaveUnits>
		{
			[new(2026, 4, 7)] = new(1m, 0m),
			[new(2026, 4, 8)] = new(0.5m, 0m)
		};

		var runPayrollPreview = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			Array.Empty<Holiday>(),
			paidLeave);
		var salarySlipLocking = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			Array.Empty<Holiday>(),
			paidLeave);

		Assert.Equal(runPayrollPreview.PayableDays, salarySlipLocking.PayableDays);
		Assert.Equal(runPayrollPreview.PresentDays, salarySlipLocking.PresentDays);
		Assert.Equal(runPayrollPreview.HalfWorkingDays, salarySlipLocking.HalfWorkingDays);
	}

	[Fact]
	public void Attendance_Payable_Days_Stay_Status_Driven_For_Attendance_Configuration_V1()
	{
		var basis = new PayrollBasisDaysResult(26, DayCalculationType.WorkingDays, false);
		var attendance = new List<EmployeeAttendance>
		{
			Attendance(2026, 4, 6, EmployeeAttendanceStatusType.Present),
			Attendance(2026, 4, 7, EmployeeAttendanceStatusType.HalfDayLeave),
			Attendance(2026, 4, 8, EmployeeAttendanceStatusType.FullDayLeave)
		};

		var beforeConfigurationPreview = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			Array.Empty<Holiday>());
		var afterConfigurationPreview = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			2026,
			4,
			Array.Empty<Holiday>());

		Assert.Equal(1, beforeConfigurationPreview.PresentDays);
		Assert.Equal(1, beforeConfigurationPreview.HalfWorkingDays);
		Assert.Equal(1.5m, beforeConfigurationPreview.PayableDays);
		Assert.Equal(beforeConfigurationPreview.PayableDays, afterConfigurationPreview.PayableDays);
		Assert.Equal(beforeConfigurationPreview.PresentDays, afterConfigurationPreview.PresentDays);
		Assert.Equal(beforeConfigurationPreview.HalfWorkingDays, afterConfigurationPreview.HalfWorkingDays);
	}

	[Fact]
	public void FullGrossSalary_Stays_Unprorated_While_Gross_Uses_Attendance_Factor()
	{
		var components = new List<EmployeePayrollComponent>
		{
			Component("Basic", 20000m, isEarning: true),
			Component("HRA", 10000m, isEarning: true),
			Component("PF", 3000m, isEarning: false)
		};

		var result = PayrollComputation.Calculate(10, 0, 10m, 20, components);

		Assert.Equal(30000m, result.FullGrossSalary);
		Assert.Equal(15000m, result.GrossSalary);
		Assert.Equal(1500m, result.TotalDeduction);
		Assert.Equal(13500m, result.NetPay);
	}

	private static EmployeeAttendance Attendance(int year, int month, int day, EmployeeAttendanceStatusType status) =>
		new()
		{
			EmployeeId = Guid.NewGuid(),
			AttendanceDate = new DateOnly(year, month, day),
			StatusType = status
		};

	private static EmployeePayrollComponent Component(string name, decimal amount, bool isEarning) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			name,
			componentId: null,
			description: null,
			amount,
			isFixed: true,
			isTemp: false,
			applicableMonth: null,
			SalaryComponentType.Fixed,
			dependOn: null,
			isEarning);
}
