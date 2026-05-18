using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DayCalculationSetting : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }

	public Guid? AcademicYearId { get; protected set; }

	public DayCalculationType DayCalculationType { get; protected set; }

	public int? ManualDays { get; protected set; }

	public string? ManualMonthDaysJson { get; protected set; }

	public SaturdayWorkPolicy SaturdayWorkPolicy { get; protected set; }

	public HolidaySyncMode HolidaySyncMode { get; protected set; }

	public string? HolidayOverridesJson { get; protected set; }

	public string? LockedMonthSettingsJson { get; protected set; }

	protected DayCalculationSetting()
	{
	}

	public DayCalculationSetting(
		Guid id,
		Guid? tenantId,
		DayCalculationType dayCalculationType,
		int? manualDays,
		Guid? academicYearId = null,
		IReadOnlyList<DayCalculationManualMonth>? manualMonthDays = null,
		SaturdayWorkPolicy saturdayWorkPolicy = SaturdayWorkPolicy.FullDay,
		HolidaySyncMode holidaySyncMode = HolidaySyncMode.SyncedWithOverrides,
		IReadOnlyList<PayrollHolidayOverride>? holidayOverrides = null,
		IReadOnlyList<PayrollLockedMonthSetting>? lockedMonthSettings = null) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SetFields(dayCalculationType, manualDays, manualMonthDays);
		SetCalendarFields(saturdayWorkPolicy, holidaySyncMode, holidayOverrides);
		SetLockedMonthSettings(lockedMonthSettings);
	}

	public void Update(
		DayCalculationType dayCalculationType,
		int? manualDays,
		IReadOnlyList<DayCalculationManualMonth>? manualMonthDays = null,
		SaturdayWorkPolicy saturdayWorkPolicy = SaturdayWorkPolicy.FullDay,
		HolidaySyncMode holidaySyncMode = HolidaySyncMode.SyncedWithOverrides,
		IReadOnlyList<PayrollHolidayOverride>? holidayOverrides = null,
		IReadOnlyList<PayrollLockedMonthSetting>? lockedMonthSettings = null)
	{
		SetFields(dayCalculationType, manualDays, manualMonthDays);
		SetCalendarFields(saturdayWorkPolicy, holidaySyncMode, holidayOverrides);
		if (lockedMonthSettings != null)
		{
			SetLockedMonthSettings(lockedMonthSettings);
		}
	}

	public IReadOnlyList<DayCalculationManualMonth> GetManualMonthDays()
	{
		if (string.IsNullOrWhiteSpace(ManualMonthDaysJson))
		{
			return Array.Empty<DayCalculationManualMonth>();
		}

		return JsonSerializer.Deserialize<List<DayCalculationManualMonth>>(ManualMonthDaysJson)
			?? (IReadOnlyList<DayCalculationManualMonth>)Array.Empty<DayCalculationManualMonth>();
	}

	public int? GetManualDaysForMonth(int year, int month)
	{
		var manualMonth = GetManualMonthDays().FirstOrDefault(x => x.Year == year && x.Month == month);
		return manualMonth?.ManualDays ?? ManualDays;
	}

	public IReadOnlyList<PayrollHolidayOverride> GetHolidayOverrides()
	{
		if (string.IsNullOrWhiteSpace(HolidayOverridesJson))
		{
			return Array.Empty<PayrollHolidayOverride>();
		}

		return JsonSerializer.Deserialize<List<PayrollHolidayOverride>>(HolidayOverridesJson)
			?? (IReadOnlyList<PayrollHolidayOverride>)Array.Empty<PayrollHolidayOverride>();
	}

	public IReadOnlyList<PayrollLockedMonthSetting> GetLockedMonthSettings()
	{
		if (string.IsNullOrWhiteSpace(LockedMonthSettingsJson))
		{
			return Array.Empty<PayrollLockedMonthSetting>();
		}

		return JsonSerializer.Deserialize<List<PayrollLockedMonthSetting>>(LockedMonthSettingsJson)
			?? (IReadOnlyList<PayrollLockedMonthSetting>)Array.Empty<PayrollLockedMonthSetting>();
	}

	public PayrollLockedMonthSetting? GetLockedMonthSetting(int year, int month) =>
		GetLockedMonthSettings().FirstOrDefault(x => x.Year == year && x.Month == month);

	public DayCalculationType GetEffectiveDayCalculationTypeForMonth(int year, int month) =>
		GetLockedMonthSetting(year, month)?.DayCalculationType ?? DayCalculationType;

	public int? GetEffectiveManualDaysForMonth(int year, int month)
	{
		var lockedMonth = GetLockedMonthSetting(year, month);
		return lockedMonth?.ManualDays ?? GetManualDaysForMonth(year, month);
	}

	public SaturdayWorkPolicy GetEffectiveSaturdayWorkPolicyForMonth(int year, int month) =>
		GetLockedMonthSetting(year, month)?.SaturdayWorkPolicy ?? SaturdayWorkPolicy;

	public IReadOnlyList<PayrollHolidayOverride> GetEffectiveHolidayOverridesForMonth(int year, int month)
	{
		var lockedMonth = GetLockedMonthSetting(year, month);
		return lockedMonth?.HolidayOverrides ?? GetHolidayOverrides();
	}

	private void SetFields(DayCalculationType dayCalculationType, int? manualDays, IReadOnlyList<DayCalculationManualMonth>? manualMonthDays)
	{
		DayCalculationType = dayCalculationType;

		if (dayCalculationType == DayCalculationType.ManualDays)
		{
			var normalizedMonths = NormalizeManualMonthDays(manualMonthDays);
			if (normalizedMonths.Count > 0)
			{
				ManualDays = null;
				ManualMonthDaysJson = JsonSerializer.Serialize(normalizedMonths);
				return;
			}

			if (manualDays.HasValue && manualDays.Value >= 1 && manualDays.Value <= 31)
			{
				ManualDays = manualDays.Value;
				ManualMonthDaysJson = null;
				return;
			}

			throw new BusinessException("ShikshaMitra:InvalidManualDays")
				.WithData("message", "manualDays or manualMonthDays must be provided when dayCalculationType is manual-days.");
		}

		ManualDays = null;
		ManualMonthDaysJson = null;
	}

	private static List<DayCalculationManualMonth> NormalizeManualMonthDays(IReadOnlyList<DayCalculationManualMonth>? manualMonthDays)
	{
		if (manualMonthDays is null || manualMonthDays.Count == 0)
		{
			return new List<DayCalculationManualMonth>();
		}

		var duplicate = manualMonthDays
			.GroupBy(x => new { x.Year, x.Month })
			.FirstOrDefault(g => g.Count() > 1);
		if (duplicate != null)
		{
			throw new BusinessException("ShikshaMitra:DuplicateManualMonthDays")
				.WithData("message", "manualMonthDays cannot contain duplicate year/month entries.");
		}

		return manualMonthDays
			.Select(x =>
			{
				if (x.Month is < 1 or > 12)
				{
					throw new BusinessException("ShikshaMitra:InvalidManualMonth")
						.WithData("message", "manualMonthDays month must be between 1 and 12.");
				}

				var calendarDays = DateTime.DaysInMonth(x.Year, x.Month);
				if (x.ManualDays < 1 || x.ManualDays > calendarDays)
				{
					throw new BusinessException("ShikshaMitra:InvalidManualDays")
						.WithData("message", $"manualDays for {x.Year}-{x.Month:00} must be between 1 and {calendarDays}.");
				}

				return new DayCalculationManualMonth(x.Year, x.Month, x.ManualDays);
			})
			.OrderBy(x => x.Year)
			.ThenBy(x => x.Month)
			.ToList();
	}

	private void SetCalendarFields(
		SaturdayWorkPolicy saturdayWorkPolicy,
		HolidaySyncMode holidaySyncMode,
		IReadOnlyList<PayrollHolidayOverride>? holidayOverrides)
	{
		SaturdayWorkPolicy = saturdayWorkPolicy;
		HolidaySyncMode = holidaySyncMode;
		var normalizedOverrides = NormalizeHolidayOverrides(holidayOverrides);
		HolidayOverridesJson = normalizedOverrides.Count == 0
			? null
			: JsonSerializer.Serialize(normalizedOverrides);
	}

	public void SetLockedMonthSettings(IReadOnlyList<PayrollLockedMonthSetting>? lockedMonthSettings)
	{
		var normalized = NormalizeLockedMonthSettings(lockedMonthSettings);
		LockedMonthSettingsJson = normalized.Count == 0
			? null
			: JsonSerializer.Serialize(normalized);
	}

	private static List<PayrollLockedMonthSetting> NormalizeLockedMonthSettings(IReadOnlyList<PayrollLockedMonthSetting>? lockedMonthSettings)
	{
		if (lockedMonthSettings is null || lockedMonthSettings.Count == 0)
		{
			return new List<PayrollLockedMonthSetting>();
		}

		var duplicate = lockedMonthSettings
			.GroupBy(x => new { x.Year, x.Month })
			.FirstOrDefault(g => g.Count() > 1);
		if (duplicate != null)
		{
			throw new BusinessException("ShikshaMitra:DuplicateLockedPayrollMonth")
				.WithData("message", "locked month settings cannot contain duplicate year/month entries.");
		}

		return lockedMonthSettings
			.Select(x =>
			{
				if (x.Month is < 1 or > 12)
				{
					throw new BusinessException("ShikshaMitra:InvalidLockedPayrollMonth")
						.WithData("message", "locked month month must be between 1 and 12.");
				}

				return new PayrollLockedMonthSetting(
					x.Year,
					x.Month,
					x.DayCalculationType,
					x.ManualDays,
					x.SaturdayWorkPolicy,
					NormalizeHolidayOverrides(x.HolidayOverrides));
			})
			.OrderBy(x => x.Year)
			.ThenBy(x => x.Month)
			.ToList();
	}

	private static List<PayrollHolidayOverride> NormalizeHolidayOverrides(IReadOnlyList<PayrollHolidayOverride>? holidayOverrides)
	{
		if (holidayOverrides is null || holidayOverrides.Count == 0)
		{
			return new List<PayrollHolidayOverride>();
		}

		var duplicate = holidayOverrides
			.GroupBy(x => new { Date = x.Date.Date, x.CalendarHolidayId })
			.FirstOrDefault(g => g.Count() > 1);
		if (duplicate != null)
		{
			throw new BusinessException("ShikshaMitra:DuplicatePayrollHolidayOverride")
				.WithData("message", "holidayOverrides cannot contain duplicate date/calendar holiday entries.");
		}

		return holidayOverrides
			.Select(x =>
			{
				var holidayName = string.IsNullOrWhiteSpace(x.HolidayName)
					? "Payroll holiday"
					: x.HolidayName.Trim();

				return new PayrollHolidayOverride(
					x.Date.Date,
					holidayName,
					x.IncludedInPayroll,
					x.CalendarHolidayId);
			})
			.OrderBy(x => x.Date)
			.ThenBy(x => x.CalendarHolidayId)
			.ToList();
	}
}

public sealed record DayCalculationManualMonth(int Year, int Month, int ManualDays);

public sealed record PayrollHolidayOverride(
	DateTime Date,
	string HolidayName,
	bool IncludedInPayroll,
	Guid? CalendarHolidayId = null);

public sealed record PayrollLockedMonthSetting(
	int Year,
	int Month,
	DayCalculationType DayCalculationType,
	int? ManualDays,
	SaturdayWorkPolicy SaturdayWorkPolicy,
	IReadOnlyList<PayrollHolidayOverride> HolidayOverrides);
