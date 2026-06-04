using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Policies;

internal static class PayrollPolicyModelBuilder
{
	public static PayrollPolicy Build(Guid id, Guid? tenantId, CreateUpdatePayrollPolicyDto input) =>
		new(
			id,
			tenantId,
			input.AcademicYearId,
			input.WorkCalendarPolicyId,
			input.Name,
			HrPolicyMapping.ParsePolicyStatus(input.Status),
			input.IsDefault,
			HrPolicyMapping.ParseDayCalculationType(input.DayCalculationType),
			input.ManualDays,
			HrPolicyMapping.ToManualMonths(input.ManualMonthDays),
			HrPolicyMapping.ParseSaturdayWorkPolicy(input.SaturdayWorkPolicy),
			HrPolicyMapping.ToHolidayOverrides(input.HolidayOverrides));

	public static bool IsUnchanged(
		PayrollPolicy entity,
		CreateUpdatePayrollPolicyDto input,
		HrPolicyStatus status,
		DayCalculationType dayCalculationType,
		IReadOnlyList<DayCalculationManualMonth> manualMonthDays,
		SaturdayWorkPolicy saturdayWorkPolicy,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides)
	{
		return entity.AcademicYearId == input.AcademicYearId &&
			entity.WorkCalendarPolicyId == input.WorkCalendarPolicyId &&
			string.Equals(entity.Name, input.Name.Trim(), StringComparison.Ordinal) &&
			entity.Status == status &&
			entity.IsDefault == input.IsDefault &&
			entity.DayCalculationType == dayCalculationType &&
			entity.ManualDays == input.ManualDays &&
			entity.SaturdayWorkPolicy == saturdayWorkPolicy &&
			ManualMonthsEqual(entity.GetManualMonthDays(), manualMonthDays) &&
			HolidayOverridesEqual(entity.GetHolidayOverrides(), holidayOverrides);
	}

	private static bool ManualMonthsEqual(
		IReadOnlyList<DayCalculationManualMonth> left,
		IReadOnlyList<DayCalculationManualMonth> right) =>
		left.Count == right.Count && left.Zip(right).All(pair =>
			pair.First.Year == pair.Second.Year &&
			pair.First.Month == pair.Second.Month &&
			pair.First.ManualDays == pair.Second.ManualDays);

	private static bool HolidayOverridesEqual(
		IReadOnlyList<PayrollHolidayOverride> left,
		IReadOnlyList<PayrollHolidayOverride> right) =>
		left.Count == right.Count && left.Zip(right).All(pair =>
			pair.First.Date.Date == pair.Second.Date.Date &&
			string.Equals(pair.First.HolidayName, pair.Second.HolidayName, StringComparison.Ordinal) &&
			pair.First.IncludedInPayroll == pair.Second.IncludedInPayroll &&
			pair.First.CalendarHolidayId == pair.Second.CalendarHolidayId);
}
