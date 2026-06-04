using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Policies;

internal static partial class HrPolicyMapping
{
	public static DayCalculationType ParseDayCalculationType(string value) => Normalize(value) switch
	{
		"total-month-days" => DayCalculationType.TotalMonthDays,
		"manual-days" => DayCalculationType.ManualDays,
		_ => DayCalculationType.WorkingDays
	};

	public static string MapDayCalculationType(DayCalculationType value) => value switch
	{
		DayCalculationType.TotalMonthDays => "total-month-days",
		DayCalculationType.ManualDays => "manual-days",
		_ => "working-days"
	};

	public static SaturdayWorkPolicy ParseSaturdayWorkPolicy(string value) => Normalize(value) switch
	{
		"half-day" => SaturdayWorkPolicy.HalfDay,
		"off" => SaturdayWorkPolicy.Off,
		_ => SaturdayWorkPolicy.FullDay
	};

	public static string MapSaturdayWorkPolicy(SaturdayWorkPolicy value) => value switch
	{
		SaturdayWorkPolicy.HalfDay => "half-day",
		SaturdayWorkPolicy.Off => "off",
		_ => "full-day"
	};

	public static PayrollPolicyDto ToDto(PayrollPolicy entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		WorkCalendarPolicyId = entity.WorkCalendarPolicyId,
		Name = entity.Name,
		Status = MapPolicyStatus(entity.Status),
		IsDefault = entity.IsDefault,
		DayCalculationType = MapDayCalculationType(entity.DayCalculationType),
		ManualDays = entity.ManualDays,
		ManualMonthDays = entity.GetManualMonthDays().Select(x => new PayrollPolicyManualMonthDto
		{
			Year = x.Year,
			Month = x.Month,
			ManualDays = x.ManualDays
		}).ToList(),
		SaturdayWorkPolicy = MapSaturdayWorkPolicy(entity.SaturdayWorkPolicy),
		HolidayOverrides = entity.GetHolidayOverrides().Select(x => new PayrollPolicyHolidayOverrideDto
		{
			Date = x.Date,
			HolidayName = x.HolidayName,
			IncludedInPayroll = x.IncludedInPayroll,
			CalendarHolidayId = x.CalendarHolidayId
		}).ToList(),
		Version = entity.Version,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId
	};

	public static IReadOnlyList<DayCalculationManualMonth> ToManualMonths(IEnumerable<PayrollPolicyManualMonthDto> input) =>
		input.Select(x => new DayCalculationManualMonth(x.Year, x.Month, x.ManualDays)).ToList();

	public static IReadOnlyList<PayrollHolidayOverride> ToHolidayOverrides(IEnumerable<PayrollPolicyHolidayOverrideDto> input) =>
		input.Select(x => new PayrollHolidayOverride(x.Date.Date, x.HolidayName, x.IncludedInPayroll, x.CalendarHolidayId)).ToList();
}
