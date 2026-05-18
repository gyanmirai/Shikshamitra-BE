using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
[Authorize]
public class DayCalculationSettingAppService : ApplicationService, IDayCalculationSettingAppService
{
	private readonly IDayCalculationSettingRepository _repository;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly PayrollCalendarResolver _payrollCalendarResolver;

	public DayCalculationSettingAppService(
		IDayCalculationSettingRepository repository,
		IAcademicYearRepository academicYearRepository,
		PayrollCalendarResolver payrollCalendarResolver)
	{
		_repository = repository;
		_academicYearRepository = academicYearRepository;
		_payrollCalendarResolver = payrollCalendarResolver;
	}

	public async Task<DayCalculationSettingDto> GetAsync(Guid academicYearId)
	{
		var academicYear = await GetAcademicYearOrThrowAsync(academicYearId);
		var setting = await _repository.FindEffectiveAsync(CurrentTenant.Id, academicYearId);
		return await BuildSettingDtoAsync(academicYear, setting, usedFallback: setting?.AcademicYearId == null);
	}

	public async Task<DayCalculationResponseDto> SaveAsync(DayCalculationRequestDto input)
	{
		var dayCalcType = ParseDayCalculationType(input.DayCalculationType);
		var saturdayWorkPolicy = ParseSaturdayWorkPolicy(input.SaturdayWorkPolicy);
		var holidaySyncMode = ParseHolidaySyncMode(input.HolidaySyncMode);
		var academicYear = input.AcademicYearId.HasValue
			? await GetAcademicYearOrThrowAsync(input.AcademicYearId.Value)
			: null;
		var manualMonths = ToManualMonths(input.ManualMonthDays);
		var holidayOverrides = academicYear != null
			? await NormalizeHolidayOverridesForAcademicYearAsync(input.HolidayOverrides, academicYear)
			: ToHolidayOverrides(input.HolidayOverrides);

		var existing = input.AcademicYearId.HasValue
			? await _repository.FindByTenantAndAcademicYearAsync(CurrentTenant.Id, input.AcademicYearId.Value)
			: await _repository.FindByTenantAsync(CurrentTenant.Id);
		var existingEffective = academicYear != null
			? await _repository.FindEffectiveAsync(CurrentTenant.Id, academicYear.Id)
			: existing;

		if (academicYear != null && dayCalcType == DayCalculationType.ManualDays)
		{
			ValidateManualMonthsForAcademicYear(manualMonths, GetAcademicYearMonths(academicYear).Where(x => !IsPastMonth(x.Year, x.Month)).ToList());
			ValidatePastManualMonthsUnchanged(manualMonths, existingEffective);
		}

		holidayOverrides = MergeAndValidateHolidayOverridesForSave(holidayOverrides, existingEffective);
		var lockedMonthSettings = academicYear != null
			? BuildLockedMonthSettings(academicYear, existingEffective)
			: existing?.GetLockedMonthSettings();

		if (existing != null)
		{
			existing.Update(dayCalcType, input.ManualDays, manualMonths, saturdayWorkPolicy, holidaySyncMode, holidayOverrides, lockedMonthSettings);
			await _repository.UpdateAsync(existing, autoSave: true);
		}
		else
		{
			existing = new DayCalculationSetting(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				dayCalcType,
				input.ManualDays,
				input.AcademicYearId,
				manualMonths,
				saturdayWorkPolicy,
				holidaySyncMode,
				holidayOverrides,
				lockedMonthSettings);
			await _repository.InsertAsync(existing, autoSave: true);
		}

		return new DayCalculationResponseDto
		{
			Success = true,
			Message = "Payroll day calculation configuration saved successfully",
			Setting = academicYear != null
				? await BuildSettingDtoAsync(academicYear, existing, usedFallback: false)
				: null
		};
	}

	public async Task<DayCalculationSettingDto> PreviewAsync(DayCalculationRequestDto input)
	{
		if (!input.AcademicYearId.HasValue)
		{
			throw new BusinessException("ShikshaMitra:AcademicYearRequired")
				.WithData("message", "academicYearId is required for payroll day calculation preview.");
		}

		var academicYear = await GetAcademicYearOrThrowAsync(input.AcademicYearId.Value);
		var dayCalcType = ParseDayCalculationType(input.DayCalculationType);
		var saturdayWorkPolicy = ParseSaturdayWorkPolicy(input.SaturdayWorkPolicy);
		var holidaySyncMode = ParseHolidaySyncMode(input.HolidaySyncMode);
		var manualMonths = ToManualMonths(input.ManualMonthDays);
		var holidayOverrides = await NormalizeHolidayOverridesForAcademicYearAsync(input.HolidayOverrides, academicYear);
		if (dayCalcType == DayCalculationType.ManualDays)
		{
			ValidateManualMonthsForAcademicYear(manualMonths, GetAcademicYearMonths(academicYear).Where(x => !IsPastMonth(x.Year, x.Month)).ToList());
		}

		var existingEffective = await _repository.FindEffectiveAsync(CurrentTenant.Id, academicYear.Id);
		var lockedMonthSettings = BuildLockedMonthSettings(academicYear, existingEffective);
		var setting = new DayCalculationSetting(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			dayCalcType,
			input.ManualDays,
			input.AcademicYearId,
			manualMonths,
			saturdayWorkPolicy,
			holidaySyncMode,
			holidayOverrides,
			lockedMonthSettings);

		return await BuildSettingDtoAsync(academicYear, setting, usedFallback: false);
	}

	private static DayCalculationType ParseDayCalculationType(string value)
	{
		return (value ?? string.Empty).Trim().ToLowerInvariant() switch
		{
			"working-days" => DayCalculationType.WorkingDays,
			"total-month-days" => DayCalculationType.TotalMonthDays,
			"manual-days" => DayCalculationType.ManualDays,
			_ => throw new BusinessException("ShikshaMitra:InvalidDayCalculationType")
				.WithData("message", "dayCalculationType must be one of: working-days, total-month-days, manual-days.")
		};
	}

	private static SaturdayWorkPolicy ParseSaturdayWorkPolicy(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? SaturdayWorkPolicy.FullDay
			: value.Trim().ToLowerInvariant() switch
			{
				"full-day" => SaturdayWorkPolicy.FullDay,
				"half-day" => SaturdayWorkPolicy.HalfDay,
				"off" => SaturdayWorkPolicy.Off,
				_ => throw new BusinessException("ShikshaMitra:InvalidSaturdayWorkPolicy")
					.WithData("message", "saturdayWorkPolicy must be one of: full-day, half-day, off.")
			};
	}

	private static HolidaySyncMode ParseHolidaySyncMode(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? HolidaySyncMode.SyncedWithOverrides
			: value.Trim().ToLowerInvariant() switch
			{
				"synced-with-overrides" => HolidaySyncMode.SyncedWithOverrides,
				_ => throw new BusinessException("ShikshaMitra:InvalidHolidaySyncMode")
					.WithData("message", "holidaySyncMode must be synced-with-overrides.")
			};
	}

	private async Task<AcademicYear> GetAcademicYearOrThrowAsync(Guid academicYearId)
	{
		var academicYear = await _academicYearRepository.FindAsync(academicYearId);
		if (academicYear == null)
		{
			throw new BusinessException("ShikshaMitra:AcademicYearNotFound")
				.WithData("message", "Academic year was not found for the current tenant.");
		}

		return academicYear;
	}

	private async Task<DayCalculationSettingDto> BuildSettingDtoAsync(AcademicYear academicYear, DayCalculationSetting? setting, bool usedFallback)
	{
		var mode = setting?.DayCalculationType ?? DayCalculationType.WorkingDays;
		var saturdayWorkPolicy = setting?.SaturdayWorkPolicy ?? SaturdayWorkPolicy.FullDay;
		var holidaySyncMode = setting?.HolidaySyncMode ?? HolidaySyncMode.SyncedWithOverrides;
		var manualMonths = setting?.GetManualMonthDays() ?? Array.Empty<DayCalculationManualMonth>();
		var holidayOverrides = setting?.GetHolidayOverrides() ?? Array.Empty<PayrollHolidayOverride>();
		var months = new List<DayCalculationMonthPreviewDto>();
		foreach (var month in GetAcademicYearMonths(academicYear))
		{
			var effectiveMode = setting?.GetEffectiveDayCalculationTypeForMonth(month.Year, month.Month) ?? mode;
			var effectiveSaturdayWorkPolicy = setting?.GetEffectiveSaturdayWorkPolicyForMonth(month.Year, month.Month) ?? saturdayWorkPolicy;
			var effectiveHolidayOverrides = setting?.GetEffectiveHolidayOverridesForMonth(month.Year, month.Month) ?? holidayOverrides;
			var calendar = await _payrollCalendarResolver.ResolveAsync(month.Year, month.Month);
			var monthHolidays = calendar.Holidays
				.Where(h => h.StartDate.Date <= month.LastDay && h.EndDate.Date >= month.FirstDay)
				.ToList();
			var resolvedHolidays = PayrollMonthCalendar.GetResolvedHolidayDaysInMonth(
				month.Year,
				month.Month,
				monthHolidays,
				effectiveHolidayOverrides);
			var workingUnitsByDate = PayrollMonthCalendar.BuildWorkingUnitsByDate(
				month.Year,
				month.Month,
				monthHolidays,
				effectiveSaturdayWorkPolicy,
				effectiveHolidayOverrides);
			var calendarDays = DateTime.DaysInMonth(month.Year, month.Month);
			var sundays = CountSundays(month.Year, month.Month);
			var saturdays = CountSaturdays(month.Year, month.Month);
			var saturdayUnits = workingUnitsByDate
				.Where(x => x.Key.DayOfWeek == DayOfWeek.Saturday)
				.Sum(x => x.Value);
			var weeklyOffDays = Enumerable.Range(1, calendarDays)
				.Select(day => new DateOnly(month.Year, month.Month, day))
				.Where(date => date.DayOfWeek is DayOfWeek.Sunday or DayOfWeek.Saturday)
				.Sum(date => 1m - GetBaseUnitForDate(date, effectiveSaturdayWorkPolicy));
			var includedHolidayDates = resolvedHolidays
				.Where(x => x.IncludedInPayroll && GetBaseUnitForDate(x.Date, effectiveSaturdayWorkPolicy) > 0m)
				.Select(x => x.Date)
				.Distinct()
				.ToList();
			var payrollHolidayUnits = resolvedHolidays
				.Where(x => x.IncludedInPayroll)
				.Select(x => x.Date)
				.Distinct()
				.Select(date => GetBaseUnitForDate(date, effectiveSaturdayWorkPolicy))
				.Sum();
			var manualDays = setting?.GetEffectiveManualDaysForMonth(month.Year, month.Month)
				?? manualMonths.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.ManualDays;
			var basisDays = effectiveMode switch
			{
				DayCalculationType.WorkingDays => workingUnitsByDate.Values.Sum(),
				DayCalculationType.TotalMonthDays => calendarDays,
				DayCalculationType.ManualDays => manualDays ?? throw new BusinessException("ShikshaMitra:ManualMonthDaysRequired")
					.WithData("message", $"manualMonthDays must include a valid entry for {month.Year}-{month.Month:00}."),
				_ => workingUnitsByDate.Values.Sum()
			};

			months.Add(new DayCalculationMonthPreviewDto
			{
				Year = month.Year,
				Month = month.Month,
				CalendarDays = calendarDays,
				Sundays = sundays,
				Saturdays = saturdays,
				SaturdayUnits = saturdayUnits,
				WeeklyOffDays = weeklyOffDays,
				HolidayDays = includedHolidayDates.Count,
				PayrollHolidayDays = includedHolidayDates.Count,
				PayrollHolidayUnits = payrollHolidayUnits,
				WorkingDays = workingUnitsByDate.Values.Sum(),
				ManualDays = manualDays,
				BasisDays = basisDays,
				DayCalculationType = PayrollDayCalculationApiLabels.ToApiString(effectiveMode),
				Locked = IsPastMonth(month.Year, month.Month),
				HolidayNames = monthHolidays
					.Where(h => resolvedHolidays.Any(r => r.CalendarHolidayId == h.Id && r.IncludedInPayroll && GetBaseUnitForDate(r.Date, effectiveSaturdayWorkPolicy) > 0m))
					.Select(h => h.HolidayName)
					.Distinct()
					.OrderBy(x => x)
					.ToList(),
				Holidays = resolvedHolidays.Select(h => new PayrollResolvedHolidayDto
				{
					Date = h.Date.ToDateTime(TimeOnly.MinValue),
					CalendarHolidayId = h.CalendarHolidayId,
					HolidayName = h.HolidayName,
					Source = h.IsPayrollOnly ? "payroll" : "calendar",
					IncludedInPayroll = h.IncludedInPayroll,
					IsEditable = !IsPastMonth(month.Year, month.Month)
				}).ToList()
			});
		}

		return new DayCalculationSettingDto
		{
			AcademicYearId = academicYear.Id,
			DayCalculationType = PayrollDayCalculationApiLabels.ToApiString(mode),
			SaturdayWorkPolicy = SaturdayWorkPolicyApiLabels.ToApiString(saturdayWorkPolicy),
			HolidaySyncMode = HolidaySyncModeApiLabels.ToApiString(holidaySyncMode),
			ManualDays = setting?.ManualDays,
			ManualMonthDays = manualMonths.Select(x => new DayCalculationManualMonthDto
			{
				Year = x.Year,
				Month = x.Month,
				ManualDays = x.ManualDays
			}).ToList(),
			HolidayOverrides = holidayOverrides.Select(ToHolidayOverrideDto).ToList(),
			UsedFallbackSetting = usedFallback,
			Months = months
		};
	}

	private static List<DayCalculationManualMonth> ToManualMonths(IEnumerable<DayCalculationManualMonthDto>? input) =>
		input?
			.Select(x => new DayCalculationManualMonth(x.Year, x.Month, x.ManualDays))
			.ToList()
		?? new List<DayCalculationManualMonth>();

	private static List<PayrollHolidayOverride> ToHolidayOverrides(IEnumerable<PayrollHolidayOverrideDto>? input) =>
		input?
			.Select(x => new PayrollHolidayOverride(
				x.Date.Date,
				x.HolidayName,
				x.IncludedInPayroll,
				x.CalendarHolidayId))
			.ToList()
		?? new List<PayrollHolidayOverride>();

	private async Task<List<PayrollHolidayOverride>> NormalizeHolidayOverridesForAcademicYearAsync(
		IEnumerable<PayrollHolidayOverrideDto>? input,
		AcademicYear academicYear)
	{
		var overrides = input?.ToList() ?? new List<PayrollHolidayOverrideDto>();
		if (overrides.Count == 0)
		{
			return new List<PayrollHolidayOverride>();
		}

		var academicMonths = GetAcademicYearMonths(academicYear)
			.Select(x => (x.Year, x.Month))
			.ToHashSet();
		var calendarsByMonth = new Dictionary<(int Year, int Month), PayrollCalendarResolution>();
		var normalized = new List<PayrollHolidayOverride>();

		foreach (var inputOverride in overrides)
		{
			var date = inputOverride.Date.Date;
			var monthKey = (date.Year, date.Month);
			if (!academicMonths.Contains(monthKey))
			{
				throw new BusinessException("ShikshaMitra:InvalidPayrollHolidayOverride")
					.WithData("message", "holidayOverrides dates must be inside the selected academic year.");
			}

			if (!calendarsByMonth.TryGetValue(monthKey, out var calendar))
			{
				calendar = await _payrollCalendarResolver.ResolveAsync(date.Year, date.Month);
				calendarsByMonth[monthKey] = calendar;
			}

			if (calendar.AcademicYearId != academicYear.Id)
			{
				throw new BusinessException("ShikshaMitra:InvalidPayrollHolidayOverride")
					.WithData("message", "holidayOverrides must match the selected academic year's tenant calendar.");
			}

			if (inputOverride.CalendarHolidayId.HasValue)
			{
				var calendarHoliday = calendar.Holidays.FirstOrDefault(h =>
					h.Id == inputOverride.CalendarHolidayId.Value &&
					h.StartDate.Date <= date &&
					h.EndDate.Date >= date);

				if (calendarHoliday == null)
				{
					throw new BusinessException("ShikshaMitra:InvalidPayrollHolidayOverride")
						.WithData("message", "calendarHolidayId must match a tenant calendar holiday on the override date.");
				}

				normalized.Add(new PayrollHolidayOverride(
					date,
					calendarHoliday.HolidayName,
					inputOverride.IncludedInPayroll,
					calendarHoliday.Id));
				continue;
			}

			if (string.IsNullOrWhiteSpace(inputOverride.HolidayName))
			{
				throw new BusinessException("ShikshaMitra:InvalidPayrollHolidayOverride")
					.WithData("message", "payroll-only holiday overrides require a holidayName.");
			}

			normalized.Add(new PayrollHolidayOverride(
				date,
				inputOverride.HolidayName.Trim(),
				inputOverride.IncludedInPayroll));
		}

		return normalized;
	}

	private static PayrollHolidayOverrideDto ToHolidayOverrideDto(PayrollHolidayOverride holidayOverride) =>
		new()
		{
			Date = holidayOverride.Date.Date,
			HolidayName = holidayOverride.HolidayName,
			IncludedInPayroll = holidayOverride.IncludedInPayroll,
			CalendarHolidayId = holidayOverride.CalendarHolidayId
		};

	private static List<(int Year, int Month, DateTime FirstDay, DateTime LastDay)> GetAcademicYearMonths(AcademicYear academicYear)
	{
		var result = new List<(int Year, int Month, DateTime FirstDay, DateTime LastDay)>();
		var cursor = new DateTime(academicYear.StartDate.Year, academicYear.StartDate.Month, 1);
		var endMonth = new DateTime(academicYear.EndDate.Year, academicYear.EndDate.Month, 1);
		while (cursor <= endMonth)
		{
			result.Add((cursor.Year, cursor.Month, cursor, new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month))));
			cursor = cursor.AddMonths(1);
		}

		return result;
	}

	private static void ValidateManualMonthsForAcademicYear(
		IReadOnlyList<DayCalculationManualMonth> manualMonths,
		IReadOnlyList<(int Year, int Month, DateTime FirstDay, DateTime LastDay)> academicMonths)
	{
		var required = academicMonths.Select(x => (x.Year, x.Month)).ToHashSet();
		var provided = manualMonths
			.Select(x => (x.Year, x.Month))
			.Where(required.Contains)
			.ToHashSet();
		if (!required.SetEquals(provided))
		{
			throw new BusinessException("ShikshaMitra:ManualMonthDaysRequired")
				.WithData("message", "manualMonthDays must include one entry for every month in the academic year.");
		}
	}

	private void ValidatePastManualMonthsUnchanged(
		IReadOnlyList<DayCalculationManualMonth> manualMonths,
		DayCalculationSetting? existing)
	{
		if (manualMonths.Count == 0)
		{
			return;
		}

		foreach (var manualMonth in manualMonths.Where(x => IsPastMonth(x.Year, x.Month)))
		{
			var existingValue = existing?.GetEffectiveManualDaysForMonth(manualMonth.Year, manualMonth.Month);
			if (existingValue != manualMonth.ManualDays)
			{
				throw new BusinessException("ShikshaMitra:PastPayrollMonthLocked")
					.WithData("message", $"Payroll configuration for {manualMonth.Year}-{manualMonth.Month:00} cannot be changed because the month is in the past.");
			}
		}
	}

	private List<PayrollHolidayOverride> MergeAndValidateHolidayOverridesForSave(
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides,
		DayCalculationSetting? existing)
	{
		var existingPastOverrides = (existing?.GetHolidayOverrides() ?? Array.Empty<PayrollHolidayOverride>())
			.Where(x => IsPastMonth(x.Date.Year, x.Date.Month))
			.ToList();
		var inputPastOverrides = holidayOverrides
			.Where(x => IsPastMonth(x.Date.Year, x.Date.Month))
			.ToList();

		var existingPastSet = existingPastOverrides
			.Select(ToHolidayOverrideComparisonKey)
			.ToHashSet();
		var inputPastSet = inputPastOverrides
			.Select(ToHolidayOverrideComparisonKey)
			.ToHashSet();

		if (inputPastOverrides.Count > 0 && !existingPastSet.SetEquals(inputPastSet))
		{
			throw new BusinessException("ShikshaMitra:PastPayrollMonthLocked")
				.WithData("message", "Payroll holiday overrides for past months cannot be changed.");
		}

		return existingPastOverrides
			.Concat(holidayOverrides.Where(x => !IsPastMonth(x.Date.Year, x.Date.Month)))
			.OrderBy(x => x.Date)
			.ThenBy(x => x.CalendarHolidayId)
			.ToList();
	}

	private static (DateTime Date, Guid? CalendarHolidayId, string HolidayName, bool IncludedInPayroll) ToHolidayOverrideComparisonKey(
		PayrollHolidayOverride holidayOverride) =>
		(
			holidayOverride.Date.Date,
			holidayOverride.CalendarHolidayId,
			holidayOverride.HolidayName,
			holidayOverride.IncludedInPayroll);

	private IReadOnlyList<PayrollLockedMonthSetting> BuildLockedMonthSettings(
		AcademicYear academicYear,
		DayCalculationSetting? existingEffective)
	{
		var academicMonthKeys = GetAcademicYearMonths(academicYear)
			.Select(x => (x.Year, x.Month))
			.ToHashSet();
		var lockedByMonth = (existingEffective?.GetLockedMonthSettings() ?? Array.Empty<PayrollLockedMonthSetting>())
			.Where(x => academicMonthKeys.Contains((x.Year, x.Month)))
			.ToDictionary(x => (x.Year, x.Month), x => x);

		foreach (var month in GetAcademicYearMonths(academicYear).Where(x => IsPastMonth(x.Year, x.Month)))
		{
			var key = (month.Year, month.Month);
			if (lockedByMonth.ContainsKey(key))
			{
				continue;
			}

			var mode = existingEffective?.GetEffectiveDayCalculationTypeForMonth(month.Year, month.Month)
				?? DayCalculationType.WorkingDays;
			lockedByMonth[key] = new PayrollLockedMonthSetting(
				month.Year,
				month.Month,
				mode,
				mode == DayCalculationType.ManualDays
					? existingEffective?.GetEffectiveManualDaysForMonth(month.Year, month.Month)
					: null,
				existingEffective?.GetEffectiveSaturdayWorkPolicyForMonth(month.Year, month.Month) ?? SaturdayWorkPolicy.FullDay,
				(existingEffective?.GetEffectiveHolidayOverridesForMonth(month.Year, month.Month) ?? Array.Empty<PayrollHolidayOverride>())
					.Where(x => x.Date.Year == month.Year && x.Date.Month == month.Month)
					.ToList());
		}

		return lockedByMonth.Values
			.OrderBy(x => x.Year)
			.ThenBy(x => x.Month)
			.ToList();
	}

	private static int CountSundays(int year, int month)
	{
		var count = 0;
		for (var date = new DateTime(year, month, 1); date.Month == month; date = date.AddDays(1))
		{
			if (date.DayOfWeek == DayOfWeek.Sunday)
			{
				count++;
			}
		}

		return count;
	}

	private static int CountSaturdays(int year, int month)
	{
		var count = 0;
		for (var date = new DateTime(year, month, 1); date.Month == month; date = date.AddDays(1))
		{
			if (date.DayOfWeek == DayOfWeek.Saturday)
			{
				count++;
			}
		}

		return count;
	}

	private static decimal GetBaseUnitForDate(DateOnly date, SaturdayWorkPolicy saturdayWorkPolicy)
	{
		return date.DayOfWeek switch
		{
			DayOfWeek.Sunday => 0m,
			DayOfWeek.Saturday => saturdayWorkPolicy switch
			{
				SaturdayWorkPolicy.FullDay => 1m,
				SaturdayWorkPolicy.HalfDay => 0.5m,
				SaturdayWorkPolicy.Off => 0m,
				_ => 1m
			},
			_ => 1m
		};
	}

	private bool IsPastMonth(int year, int month)
	{
		var currentMonth = new DateTime(Clock.Now.Year, Clock.Now.Month, 1);
		var candidateMonth = new DateTime(year, month, 1);
		return candidateMonth < currentMonth;
	}
}

public static class SaturdayWorkPolicyApiLabels
{
	public static string ToApiString(SaturdayWorkPolicy type) => type switch
	{
		SaturdayWorkPolicy.FullDay => "full-day",
		SaturdayWorkPolicy.HalfDay => "half-day",
		SaturdayWorkPolicy.Off => "off",
		_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
	};
}

public static class HolidaySyncModeApiLabels
{
	public static string ToApiString(HolidaySyncMode type) => type switch
	{
		HolidaySyncMode.SyncedWithOverrides => "synced-with-overrides",
		_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
	};
}
