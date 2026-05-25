using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Policies;

public class PayrollPolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public Guid WorkCalendarPolicyId { get; protected set; }
	public string Name { get; protected set; } = string.Empty;
	public HrPolicyStatus Status { get; protected set; }
	public bool IsDefault { get; protected set; }
	public DayCalculationType DayCalculationType { get; protected set; }
	public int? ManualDays { get; protected set; }
	public string? ManualMonthDaysJson { get; protected set; }
	public SaturdayWorkPolicy SaturdayWorkPolicy { get; protected set; }
	public string? HolidayOverridesJson { get; protected set; }
	public string? LockedMonthSettingsJson { get; protected set; }
	public int Version { get; protected set; }

	protected PayrollPolicy()
	{
	}

	public PayrollPolicy(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid workCalendarPolicyId,
		string name,
		HrPolicyStatus status,
		bool isDefault,
		DayCalculationType dayCalculationType,
		int? manualDays,
		IReadOnlyList<DayCalculationManualMonth>? manualMonthDays,
		SaturdayWorkPolicy saturdayWorkPolicy,
		IReadOnlyList<PayrollHolidayOverride>? holidayOverrides,
		IReadOnlyList<PayrollLockedMonthSetting>? lockedMonthSettings = null) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		WorkCalendarPolicyId = workCalendarPolicyId;
		Version = 0;
		Update(name, status, isDefault, dayCalculationType, manualDays, manualMonthDays, saturdayWorkPolicy, holidayOverrides, lockedMonthSettings);
	}

	public void Update(
		string name,
		HrPolicyStatus status,
		bool isDefault,
		DayCalculationType dayCalculationType,
		int? manualDays,
		IReadOnlyList<DayCalculationManualMonth>? manualMonthDays,
		SaturdayWorkPolicy saturdayWorkPolicy,
		IReadOnlyList<PayrollHolidayOverride>? holidayOverrides,
		IReadOnlyList<PayrollLockedMonthSetting>? lockedMonthSettings = null)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new BusinessException("ShikshaMitra:PayrollPolicyNameRequired");
		}

		if (isDefault && status != HrPolicyStatus.Active)
		{
			throw new BusinessException("ShikshaMitra:DefaultPolicyMustBeActive");
		}

		Name = name.Trim();
		Status = status;
		IsDefault = isDefault;
		DayCalculationType = dayCalculationType;
		SaturdayWorkPolicy = saturdayWorkPolicy;
		SetManualDays(dayCalculationType, manualDays, manualMonthDays);
		HolidayOverridesJson = SerializeOrNull(NormalizeHolidayOverrides(holidayOverrides));
		LockedMonthSettingsJson = lockedMonthSettings is null ? LockedMonthSettingsJson : SerializeOrNull(lockedMonthSettings);
		Version++;
	}

	public void SetDefault(bool isDefault)
	{
		if (isDefault && Status != HrPolicyStatus.Active)
		{
			throw new BusinessException("ShikshaMitra:DefaultPolicyMustBeActive");
		}

		IsDefault = isDefault;
		Version++;
	}

	public void Archive()
	{
		if (IsDefault)
		{
			throw new BusinessException("ShikshaMitra:DefaultPolicyCannotBeArchived");
		}

		Status = HrPolicyStatus.Archived;
		Version++;
	}

	public void Unarchive()
	{
		Status = HrPolicyStatus.Draft;
		IsDefault = false;
		Version++;
	}

	public IReadOnlyList<DayCalculationManualMonth> GetManualMonthDays() =>
		DeserializeList<DayCalculationManualMonth>(ManualMonthDaysJson);

	public int? GetManualDaysForMonth(int year, int month) =>
		GetManualMonthDays().FirstOrDefault(x => x.Year == year && x.Month == month)?.ManualDays ?? ManualDays;

	public IReadOnlyList<PayrollHolidayOverride> GetHolidayOverrides() =>
		DeserializeList<PayrollHolidayOverride>(HolidayOverridesJson);

	public IReadOnlyList<PayrollLockedMonthSetting> GetLockedMonthSettings() =>
		DeserializeList<PayrollLockedMonthSetting>(LockedMonthSettingsJson);

	private void SetManualDays(
		DayCalculationType dayCalculationType,
		int? manualDays,
		IReadOnlyList<DayCalculationManualMonth>? manualMonthDays)
	{
		if (dayCalculationType != DayCalculationType.ManualDays)
		{
			ManualDays = null;
			ManualMonthDaysJson = null;
			return;
		}

		var normalizedMonths = manualMonthDays?.OrderBy(x => x.Year).ThenBy(x => x.Month).ToList() ?? new List<DayCalculationManualMonth>();
		if (normalizedMonths.Count > 0)
		{
			ManualDays = null;
			ManualMonthDaysJson = SerializeOrNull(normalizedMonths);
			return;
		}

		if (manualDays is >= 1 and <= 31)
		{
			ManualDays = manualDays;
			ManualMonthDaysJson = null;
			return;
		}

		throw new BusinessException("ShikshaMitra:InvalidManualDays");
	}

	private static List<PayrollHolidayOverride> NormalizeHolidayOverrides(IReadOnlyList<PayrollHolidayOverride>? holidayOverrides) =>
		holidayOverrides?
			.Select(x => new PayrollHolidayOverride(x.Date.Date, x.HolidayName?.Trim() ?? "Payroll holiday", x.IncludedInPayroll, x.CalendarHolidayId))
			.OrderBy(x => x.Date)
			.ThenBy(x => x.CalendarHolidayId)
			.ToList()
		?? new List<PayrollHolidayOverride>();

	private static string? SerializeOrNull<T>(IReadOnlyList<T>? values) =>
		values is null || values.Count == 0 ? null : JsonSerializer.Serialize(values);

	private static IReadOnlyList<T> DeserializeList<T>(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return Array.Empty<T>();
		}

		return JsonSerializer.Deserialize<List<T>>(json) ?? (IReadOnlyList<T>)Array.Empty<T>();
	}
}
