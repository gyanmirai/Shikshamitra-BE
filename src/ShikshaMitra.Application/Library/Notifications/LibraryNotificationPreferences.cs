using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Policies;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationPreferences(ISettingManager settings, ICurrentTenant tenant,
	LibraryCalendarService calendar, IClock clock) : ITransientDependency
{
	public async Task<LibraryNotificationSetupDto> GetSetupAsync()
	{
		var current = await calendar.GetSettingsAsync();
		var localTime = await ReadExactAsync(LibraryNotificationSettingNames.AutomaticReminderTimeLocal);
		var routing = await ReadExactAsync(LibraryNotificationSettingNames.StudentInboxRouting);
		var result = new LibraryNotificationSetupDto
		{
			TimeZoneId = current.TimeZoneId, AutomaticReminderTimeLocal = localTime,
			StudentInboxRouting = string.IsNullOrWhiteSpace(routing) ? LibraryStudentInboxRouting.Guardians : routing,
			OperationalRecipientUserIds = await GetOperationalIdsAsync()
		};
		if (current.Source != "Library" || current.NeedsConfiguration)
			result.ConfigurationIssues.Add("Confirm a valid Library timezone before enabling automatic reminders.");
		if (!TryTime(localTime, out _))
			result.ConfigurationIssues.Add("Save a local reminder send time in HH:mm format before enabling automatic reminders.");
		if (!IsRouting(result.StudentInboxRouting))
			result.ConfigurationIssues.Add("Choose a supported student inbox routing option.");
		result.NeedsConfiguration = result.ConfigurationIssues.Count > 0;
		return result;
	}

	public async Task<(DateTime? Date, string? BlockedReason, string? ConfigurationFingerprint)> GetAutomaticDateAsync()
	{
		var setup = await GetSetupAsync();
		if (setup.NeedsConfiguration) return (null, string.Join(" ", setup.ConfigurationIssues), JsonSerializer.Serialize(new
			{ setup.TimeZoneId, setup.AutomaticReminderTimeLocal, setup.StudentInboxRouting, setup.ConfigurationIssues }));
		var local = TimeZoneInfo.ConvertTimeFromUtc(clock.Now.ToUniversalTime(), LibraryCalendarService.ResolveZone(setup.TimeZoneId));
		TryTime(setup.AutomaticReminderTimeLocal, out var sendAt);
		return TimeOnly.FromDateTime(local) >= sendAt ? (local.Date, null, null) : (null, null, null);
	}

	public async Task<DateTime> GetSchoolDateAsync()
	{
		var current = await calendar.GetSettingsAsync();
		if (current.Source != "Library" || current.NeedsConfiguration)
			throw new BusinessException(LibraryErrorCodes.CalendarNotConfigured, "Confirm the Library timezone or provide an explicit manual run date.");
		return (await calendar.TodayAsync()).ToDateTime(TimeOnly.MinValue);
	}

	public async Task<string> GetRoutingAsync()
	{
		var routing = await ReadExactAsync(LibraryNotificationSettingNames.StudentInboxRouting);
		return IsRouting(routing) ? routing! : LibraryStudentInboxRouting.Guardians;
	}

	public async Task<List<Guid>> GetOperationalIdsAsync()
	{
		var value = await ReadExactAsync(LibraryNotificationSettingNames.OperationalRecipientUserIds);
		if (string.IsNullOrWhiteSpace(value)) return [];
		try { return (JsonSerializer.Deserialize<List<Guid>>(value) ?? []).Where(id => id != Guid.Empty).Distinct().ToList(); }
		catch (JsonException) { return []; }
	}

	public async Task SaveAsync(UpdateLibraryNotificationSettingsRequest input)
	{
		Validate(input);
		if (input.AutomaticReminderTimeLocal != null)
		{
			var value = input.AutomaticReminderTimeLocal.Trim();
			if (value.Length > 0 && !TryTime(value, out _)) throw Invalid("Use HH:mm for the local reminder send time.");
			await WriteExactAsync(LibraryNotificationSettingNames.AutomaticReminderTimeLocal, value.Length == 0 ? null : value);
		}
		if (input.StudentInboxRouting != null)
		{
			if (!IsRouting(input.StudentInboxRouting)) throw Invalid("Choose a supported student inbox routing option.");
			await WriteExactAsync(LibraryNotificationSettingNames.StudentInboxRouting, input.StudentInboxRouting);
		}
		if (input.OperationalRecipientUserIds != null)
			await WriteExactAsync(LibraryNotificationSettingNames.OperationalRecipientUserIds,
				JsonSerializer.Serialize(input.OperationalRecipientUserIds.Where(id => id != Guid.Empty).Distinct().OrderBy(id => id)));
	}

	public static void Validate(UpdateLibraryNotificationSettingsRequest input)
	{
		if (input.AutomaticReminderTimeLocal is { Length: > 0 } value && !TryTime(value.Trim(), out _))
			throw Invalid("Use HH:mm for the local reminder send time.");
		if (input.StudentInboxRouting != null && !IsRouting(input.StudentInboxRouting))
			throw Invalid("Choose a supported student inbox routing option.");
		if (input.OperationalRecipientUserIds is { Count: > 100 } || input.OperationalRecipientUserIds?.Contains(Guid.Empty) == true)
			throw Invalid("Choose up to 100 valid operational recipient accounts.");
	}

	public Task<string?> ReadExactAsync(string name) => tenant.Id.HasValue
		? settings.GetOrNullForTenantAsync(name, tenant.Id.Value, fallback: false)
		: settings.GetOrNullGlobalAsync(name, fallback: false);

	private Task WriteExactAsync(string name, string? value) => tenant.Id.HasValue
		? settings.SetForTenantAsync(tenant.Id.Value, name, value, forceToSet: true)
		: settings.SetGlobalAsync(name, value);

	public static bool IsRouting(string? value) => value is LibraryStudentInboxRouting.Guardians or
		LibraryStudentInboxRouting.StudentAndGuardians or LibraryStudentInboxRouting.Student;
	private static bool TryTime(string? value, out TimeOnly time) =>
		TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
	private static BusinessException Invalid(string message) => new(LibraryNotificationErrorCodes.ConfigurationInvalid, message);
}
