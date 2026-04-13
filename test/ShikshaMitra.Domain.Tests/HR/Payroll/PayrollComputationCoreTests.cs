using System;
using ShikshaMitra.HR.Calendar.Holidays;
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
		Assert.Equal(21, n);
	}
}