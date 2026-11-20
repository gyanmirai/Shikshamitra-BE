using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Policies;

public class LibraryCalendarService(ISettingManager settings, ICurrentTenant tenant, IClock clock,
	IRepository<LibraryTiming, Guid> timings, IRepository<LibraryCalendarClosure, Guid> closures) : ITransientDependency
{
	public async Task<LibraryCalendarSettingsDto> GetSettingsAsync()
	{
		var configured = (tenant.Id.HasValue
			? await settings.GetOrNullForTenantAsync(LibraryPolicySettingNames.CalendarTimeZone, tenant.Id.Value, fallback: false)
			: await settings.GetOrNullGlobalAsync(LibraryPolicySettingNames.CalendarTimeZone, fallback: false))?.Trim();
		if (!string.IsNullOrWhiteSpace(configured))
			return new() { TimeZoneId = configured, Source = "Library", NeedsConfiguration = !IsValidZone(configured) };
		var legacy = (tenant.Id.HasValue
			? await settings.GetOrNullForTenantAsync(LibraryPolicySettingNames.LegacyHrTimeZone, tenant.Id.Value)
			: await settings.GetOrNullGlobalAsync(LibraryPolicySettingNames.LegacyHrTimeZone))?.Trim();
		return new()
		{
			TimeZoneId = string.IsNullOrWhiteSpace(legacy) ? null : legacy,
			Source = string.IsNullOrWhiteSpace(legacy) ? "Unconfigured" : "LegacyHr", NeedsConfiguration = true
		};
	}

	public async Task<DateOnly> TodayAsync()
	{
		var current = await GetSettingsAsync();
		return LibraryCalendarCalculator.SchoolDate(new DateTimeOffset(clock.Now), ResolveZone(current.TimeZoneId));
	}

	public async Task EnsureConfiguredAsync(Guid academicYearId)
	{
		var current = await GetSettingsAsync();
		if (current.Source != "Library" || current.NeedsConfiguration)
			throw new BusinessException(LibraryErrorCodes.CalendarNotConfigured, "Confirm the Library timezone before publishing an automated borrowing policy.");
		if ((await GetOpenWeekdaysAsync(academicYearId)).Count == 0)
			throw new BusinessException(LibraryErrorCodes.CalendarNoOpenDay, "Configure Library opening hours before publishing an automated borrowing policy.");
	}

	public async Task<LibraryCalendarCalculationDto> CalculateAsync(LibraryCalendarCalculateRequest input)
	{
		var current = await GetSettingsAsync();
		ResolveZone(current.TimeZoneId);
		var openDays = await GetOpenWeekdaysAsync(input.AcademicYearId);
		var applicable = await closures.GetListAsync(x => x.SectionId == null || x.SectionId == input.SectionId);
		var due = LibraryCalendarCalculator.Calculate(input.StartDate, input.Days, (LibraryDayCountingMode)input.DayCountingMode,
			input.MoveToNextOpenDay, openDays, applicable.Select(x => new LibraryClosedDateRange(x.FromDate, x.ToDate)).ToList());
		return new()
		{
			DueDate = due, TimeZoneId = current.TimeZoneId, Source = current.Source,
			NeedsConfiguration = current.NeedsConfiguration, HasValidOpenWeekday = openDays.Count > 0,
			CanPublishAutomatedPolicy = !current.NeedsConfiguration && openDays.Count > 0,
			ConfigurationIssues = current.NeedsConfiguration ? ["Confirm the Library timezone before publishing an automated policy."] : []
		};
	}

	public async Task<IReadOnlyCollection<DayOfWeek>> GetOpenWeekdaysAsync(Guid academicYearId)
	{
		var rows = await timings.GetListAsync(x => x.AcademicYearId == academicYearId);
		var days = new Dictionary<DayOfWeek, bool>();
		foreach (var row in rows)
		{
			if (!Enum.TryParse<DayOfWeek>(row.Day?.Trim(), true, out var day) || !Enum.IsDefined(day) || row.Status is < 0 or > 2)
				throw new BusinessException(LibraryErrorCodes.CalendarInvalid, "Library hours contain an invalid weekday or status.");
			var isOpen = row.Status == 0;
			if (isOpen && (!TimeSpan.TryParse(row.StartTime, out var start) || !TimeSpan.TryParse(row.EndTime, out var end) ||
				start < TimeSpan.Zero || end > TimeSpan.FromDays(1) || start >= end))
				throw new BusinessException(LibraryErrorCodes.CalendarInvalid, "Open Library hours must have valid opening and closing times.");
			if (days.TryGetValue(day, out var existing) && existing != isOpen)
				throw new BusinessException(LibraryErrorCodes.CalendarInvalid, "Library hours contain conflicting entries for the same weekday.");
			days[day] = isOpen;
		}
		return days.Where(x => x.Value).Select(x => x.Key).ToList();
	}

	public static TimeZoneInfo ResolveZone(string? id)
	{
		if (string.IsNullOrWhiteSpace(id))
			throw new BusinessException(LibraryErrorCodes.CalendarNotConfigured, "Configure the Library timezone before calculating school dates.");
		try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
		catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
		{
			throw new BusinessException(LibraryErrorCodes.CalendarInvalid, "The configured timezone is invalid. Update Library calendar settings.");
		}
	}
	private static bool IsValidZone(string id)
	{
		try { ResolveZone(id); return true; }
		catch (BusinessException) { return false; }
	}
}
