using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public class PayrollPolicy_Tests
{
	[Fact]
	public void Should_Reject_Duplicate_Manual_Month_Days()
	{
		var ex = Should.Throw<BusinessException>(() => CreatePolicy(
			manualMonthDays:
			[
				new DayCalculationManualMonth(2026, 4, 26),
				new DayCalculationManualMonth(2026, 4, 27)
			]));

		ex.Code.ShouldBe(HRErrorCodes.DuplicateManualMonthDays);
	}

	[Fact]
	public void Should_Reject_Duplicate_Holiday_Overrides()
	{
		var date = new DateTime(2026, 5, 1);

		var ex = Should.Throw<BusinessException>(() => CreatePolicy(
			dayCalculationType: DayCalculationType.WorkingDays,
			manualDays: null,
			holidayOverrides:
			[
				new PayrollHolidayOverride(date, "May Day", true, Guid.NewGuid()),
				new PayrollHolidayOverride(date, "May Day duplicate", false, Guid.NewGuid())
			]));

		ex.Code.ShouldBe(HRErrorCodes.DuplicatePayrollHolidayOverride);
	}

	private static PayrollPolicy CreatePolicy(
		DayCalculationType dayCalculationType = DayCalculationType.ManualDays,
		int? manualDays = null,
		IReadOnlyList<DayCalculationManualMonth>? manualMonthDays = null,
		IReadOnlyList<PayrollHolidayOverride>? holidayOverrides = null) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			"Payroll Policy",
			HrPolicyStatus.Draft,
			false,
			dayCalculationType,
			manualDays,
			manualMonthDays,
			SaturdayWorkPolicy.FullDay,
			holidayOverrides);
}
