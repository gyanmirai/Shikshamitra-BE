using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Payroll.Services;

public static class PayrollBasisDays
{
	public static PayrollBasisDaysResult ResolvePolicy(
		PayrollPolicy policy,
		int year,
		int month,
		decimal workingWeekdaysMinusHolidayWeekdays,
		IReadOnlyDictionary<DateOnly, decimal>? workingUnitsByDate = null)
	{
		return policy.DayCalculationType switch
		{
			DayCalculationType.WorkingDays => new PayrollBasisDaysResult(
				workingWeekdaysMinusHolidayWeekdays,
				DayCalculationType.WorkingDays,
				UsedTenantDefault: false,
				policy.SaturdayWorkPolicy,
				workingUnitsByDate),

			DayCalculationType.TotalMonthDays => new PayrollBasisDaysResult(
				DateTime.DaysInMonth(year, month),
				DayCalculationType.TotalMonthDays,
				UsedTenantDefault: false,
				policy.SaturdayWorkPolicy,
				workingUnitsByDate),

			DayCalculationType.ManualDays => new PayrollBasisDaysResult(
				policy.GetManualDaysForMonth(year, month)
					?? throw new InvalidOperationException($"ManualDays must be set for {year}-{month:00} when mode is ManualDays."),
				DayCalculationType.ManualDays,
				UsedTenantDefault: false,
				policy.SaturdayWorkPolicy,
				workingUnitsByDate),

			_ => throw new ArgumentOutOfRangeException(nameof(policy), policy.DayCalculationType, "Unknown day calculation type.")
		};
	}
}
