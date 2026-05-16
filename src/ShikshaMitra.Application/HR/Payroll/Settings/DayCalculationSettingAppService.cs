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
		var academicYear = await _academicYearRepository.GetAsync(academicYearId);
		var setting = await _repository.FindEffectiveAsync(CurrentTenant.Id, academicYearId);
		return await BuildSettingDtoAsync(academicYear, setting, usedFallback: setting?.AcademicYearId == null);
	}

	public async Task<DayCalculationResponseDto> SaveAsync(DayCalculationRequestDto input)
	{
		var dayCalcType = ParseDayCalculationType(input.DayCalculationType);
		var academicYear = input.AcademicYearId.HasValue
			? await _academicYearRepository.GetAsync(input.AcademicYearId.Value)
			: null;
		var manualMonths = ToManualMonths(input.ManualMonthDays);

		if (academicYear != null && dayCalcType == DayCalculationType.ManualDays)
		{
			ValidateManualMonthsForAcademicYear(manualMonths, GetAcademicYearMonths(academicYear));
		}

		var existing = input.AcademicYearId.HasValue
			? await _repository.FindByTenantAndAcademicYearAsync(CurrentTenant.Id, input.AcademicYearId.Value)
			: await _repository.FindByTenantAsync(CurrentTenant.Id);

		if (existing != null)
		{
			existing.Update(dayCalcType, input.ManualDays, manualMonths);
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
				manualMonths);
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

		var academicYear = await _academicYearRepository.GetAsync(input.AcademicYearId.Value);
		var dayCalcType = ParseDayCalculationType(input.DayCalculationType);
		var manualMonths = ToManualMonths(input.ManualMonthDays);
		if (dayCalcType == DayCalculationType.ManualDays)
		{
			ValidateManualMonthsForAcademicYear(manualMonths, GetAcademicYearMonths(academicYear));
		}

		var setting = new DayCalculationSetting(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			dayCalcType,
			input.ManualDays,
			input.AcademicYearId,
			manualMonths);

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

	private async Task<DayCalculationSettingDto> BuildSettingDtoAsync(AcademicYear academicYear, DayCalculationSetting? setting, bool usedFallback)
	{
		var mode = setting?.DayCalculationType ?? DayCalculationType.WorkingDays;
		var manualMonths = setting?.GetManualMonthDays() ?? Array.Empty<DayCalculationManualMonth>();
		var months = new List<DayCalculationMonthPreviewDto>();
		foreach (var month in GetAcademicYearMonths(academicYear))
		{
			var calendar = await _payrollCalendarResolver.ResolveAsync(month.Year, month.Month);
			var monthHolidays = calendar.Holidays
				.Where(h => h.StartDate.Date <= month.LastDay && h.EndDate.Date >= month.FirstDay)
				.ToList();
			var calendarDays = DateTime.DaysInMonth(month.Year, month.Month);
			var sundays = CountSundays(month.Year, month.Month);
			var holidayDates = PayrollMonthCalendar.GetHolidayDateOnlySetInMonth(month.Year, month.Month, monthHolidays)
				.Where(date => date.DayOfWeek != DayOfWeek.Sunday)
				.ToList();
			var manualDays = manualMonths.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.ManualDays
				?? setting?.ManualDays;
			var basisDays = mode switch
			{
				DayCalculationType.WorkingDays => calendar.WorkingDays,
				DayCalculationType.TotalMonthDays => calendarDays,
				DayCalculationType.ManualDays => manualDays ?? throw new BusinessException("ShikshaMitra:ManualMonthDaysRequired")
					.WithData("message", $"manualMonthDays must include a valid entry for {month.Year}-{month.Month:00}."),
				_ => calendar.WorkingDays
			};

			months.Add(new DayCalculationMonthPreviewDto
			{
				Year = month.Year,
				Month = month.Month,
				CalendarDays = calendarDays,
				Sundays = sundays,
				HolidayDays = holidayDates.Count,
				WorkingDays = calendar.WorkingDays,
				ManualDays = manualDays,
				BasisDays = basisDays,
				DayCalculationType = PayrollDayCalculationApiLabels.ToApiString(mode),
				HolidayNames = monthHolidays
					.Where(h => PayrollMonthCalendar.GetHolidayDateOnlySetInMonth(month.Year, month.Month, new[] { h }).Any(d => d.DayOfWeek != DayOfWeek.Sunday))
					.Select(h => h.HolidayName)
					.Distinct()
					.OrderBy(x => x)
					.ToList()
			});
		}

		return new DayCalculationSettingDto
		{
			AcademicYearId = academicYear.Id,
			DayCalculationType = PayrollDayCalculationApiLabels.ToApiString(mode),
			ManualDays = setting?.ManualDays,
			ManualMonthDays = manualMonths.Select(x => new DayCalculationManualMonthDto
			{
				Year = x.Year,
				Month = x.Month,
				ManualDays = x.ManualDays
			}).ToList(),
			UsedFallbackSetting = usedFallback,
			Months = months
		};
	}

	private static List<DayCalculationManualMonth> ToManualMonths(IEnumerable<DayCalculationManualMonthDto>? input) =>
		input?
			.Select(x => new DayCalculationManualMonth(x.Year, x.Month, x.ManualDays))
			.ToList()
		?? new List<DayCalculationManualMonth>();

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
		var provided = manualMonths.Select(x => (x.Year, x.Month)).ToHashSet();
		if (!required.SetEquals(provided))
		{
			throw new BusinessException("ShikshaMitra:ManualMonthDaysRequired")
				.WithData("message", "manualMonthDays must include one entry for every month in the academic year.");
		}
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
}
