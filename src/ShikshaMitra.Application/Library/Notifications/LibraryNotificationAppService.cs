using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Library.Books;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Notifications;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Notification.View)]
[RemoteService(false)]
public class LibraryNotificationAppService(
	IRepository<LibraryNotificationSetting, Guid> settingRepository,
	IRepository<LibraryNotificationHistory, Guid> historyRepository,
	LibraryNotificationCatalog catalog,
	LibraryNotificationDeliveryService deliveryService,
	LibraryNotificationReminderService reminderService,
	IBackgroundJobManager jobs,
	ILibraryInventoryLock inventoryLock,
	ILibraryAccessService libraryAccess,
	LibraryNotificationPreferences preferences,
	LibraryConfiguredOperationalRecipientReader configuredRecipients,
	ILibraryNotificationAuthorityService authority,
	IRepository<IdentityUser, Guid> users,
	IUserScopeService userScopes,
	LibraryNotificationRecordScope recordScope,
	LibraryNotificationCandidateScope candidates,
	IUnitOfWorkManager units)
	: ApplicationService, ILibraryNotificationAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Notification.Add)]
	public async Task<List<LibraryOperationalRecipientOptionDto>> GetOperationalRecipientsAsync(string? filter = null)
	{
		if (!CurrentTenant.Id.HasValue || !await authority.CanManageSettingsAsync()) throw new AbpAuthorizationException();
		var search = filter?.Trim();
		if (string.IsNullOrEmpty(search) || search.Length < 2) return [];
		var normalized = search.ToUpperInvariant();
		using var read = units.Begin(requiresNew: true, isTransactional: false);
		var query = (await users.GetQueryableAsync()).Where(user => user.TenantId == CurrentTenant.Id && user.IsActive &&
			((user.NormalizedUserName != null && user.NormalizedUserName.Contains(normalized)) || (user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalized)) ||
			 (user.Name != null && user.Name.ToUpper().Contains(normalized)) || (user.Surname != null && user.Surname.ToUpper().Contains(normalized))));
		var result = new List<LibraryOperationalRecipientOptionDto>();
		for (var offset = 0; result.Count < 50; offset += 100)
		{
			var candidates = await AsyncExecuter.ToListAsync(query.OrderBy(user => user.UserName).ThenBy(user => user.Id).Skip(offset).Take(100));
			if (candidates.Count == 0) break;
			var scopes = await userScopes.GetUserScopesAsync(candidates.Select(user => user.Id).ToArray());
			foreach (var user in candidates)
			{
				if (result.Count == 50) break;
				if (!scopes.TryGetValue(user.Id, out var scope) || !scope.HasTenantWideScope() || !await authority.CanReceiveOperationalAsync(user.Id)) continue;
				var displayName = string.Join(" ", new[] { user.Name, user.Surname }.Where(part => !string.IsNullOrWhiteSpace(part)));
				result.Add(new() { UserId = user.Id, UserName = user.UserName, Email = user.Email,
					DisplayName = string.IsNullOrWhiteSpace(displayName) ? user.UserName : displayName });
			}
			if (candidates.Count < 100) break;
		}
		if (!await authority.CanManageSettingsAsync()) throw new AbpAuthorizationException();
		await read.CompleteAsync();
		return result;
	}

	public async Task<LibraryNotificationSettingsDto> GetSettingsAsync()
	{
		var settings = await deliveryService.EnsureSettingsAsync();
		var latestHistory = await GetLatestHistoryBySettingKeyAsync();
		var canManageSettings = await authority.CanManageSettingsAsync();
		var setup = await preferences.GetSetupAsync();
		if (canManageSettings)
			setup.ConfiguredOperationalRecipients = await configuredRecipients.ReadAsync(setup.OperationalRecipientUserIds);

		return new LibraryNotificationSettingsDto
		{
			CanManageSettings = canManageSettings,
			Setup = setup,
			Items = settings.Select(setting => MapSetting(setting, latestHistory.GetValueOrDefault(
				LibraryNotificationDeliveryService.BuildSettingKey(setting.EventType, setting.RecipientRole, setting.Channel)))).ToList(),
			Events = catalog.GetDefinitions()
				.Select(x => new LibraryNotificationEventOptionDto
				{
					EventType = x.EventType,
					EventGroup = x.EventGroup,
					EventName = x.EventName,
					RecipientRole = x.RecipientRole,
					IsComingSoon = x.IsComingSoon
				})
				.ToList(),
			Channels = catalog.GetChannels()
				.Select(x => new LibraryNotificationChannelOptionDto
				{
					Channel = x.Channel,
					Label = x.Label,
					IsAvailable = x.IsAvailable
				})
				.ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Notification.Add)]
	public async Task<LibraryNotificationSettingsDto> UpdateSettingsAsync(UpdateLibraryNotificationSettingsRequest input)
	{
		if (!await authority.CanManageSettingsAsync()) throw new AbpAuthorizationException();
		LibraryNotificationPreferences.Validate(input);
		foreach (var userId in input.OperationalRecipientUserIds ?? [])
			if (!await authority.CanReceiveOperationalAsync(userId)) throw new AbpAuthorizationException();
		if (input.Items.Any(item => item.RepeatIntervalDays is < 1 or > 365 || item.ReminderOffsetDays is < 0 or > 365 ||
			item.SubjectTemplate?.Trim().Length > 300 || item.MessageTemplate?.Trim().Length > 2000))
			throw new BusinessException(LibraryNotificationErrorCodes.ConfigurationInvalid, "Review reminder intervals and template lengths.");
		var settings = await deliveryService.EnsureSettingsAsync();

		foreach (var update in input.Items)
		{
			var setting = ResolveSetting(settings, update);
			if (setting == null)
			{
				throw new BusinessException(code: LibraryErrorCodes.NotificationEventNotSupported, message: "Library notification setting was not found.");
			}

			if (update.IsActive == true && (!setting.IsChannelAvailable || setting.IsComingSoon))
			{
				throw new BusinessException(code: LibraryErrorCodes.NotificationChannelDisabled, message: "This Library notification channel or event is not available in this phase.");
			}

			if (update.IsActive.HasValue)
			{
				setting.IsActive = update.IsActive.Value;
			}

			if (update.ReminderOffsetDays.HasValue)
			{
				setting.ReminderOffsetDays = Math.Max(0, update.ReminderOffsetDays.Value);
			}
			if (update.RepeatIntervalDays.HasValue) setting.RepeatIntervalDays = update.RepeatIntervalDays.Value;

			if (update.SubjectTemplate != null)
			{
				setting.SubjectTemplate = update.SubjectTemplate.Trim();
			}

			if (update.MessageTemplate != null)
			{
				setting.MessageTemplate = update.MessageTemplate.Trim();
			}

			await settingRepository.UpdateAsync(setting, autoSave: true);
		}

		await preferences.SaveAsync(input);
		return await GetSettingsAsync();
	}

	public async Task<PaginatedResponse<LibraryNotificationHistoryDto>> GetHistoryAsync(LibraryNotificationHistoryRequest input)
	{
		var page = Math.Max(input.Page, 1);
		var pageSize = Math.Clamp(input.PageSize, 1, 100);
		var query = await GetScopedHistoryQueryAsync();

		if (!string.IsNullOrWhiteSpace(input.EventType))
		{
			query = query.Where(x => x.EventType == input.EventType);
		}

		if (!string.IsNullOrWhiteSpace(input.EventGroup))
		{
			query = query.Where(x => x.EventGroup == input.EventGroup);
		}

		if (!string.IsNullOrWhiteSpace(input.Channel))
		{
			query = query.Where(x => x.Channel == input.Channel);
		}

		if (!string.IsNullOrWhiteSpace(input.Status))
		{
			query = query.Where(x => x.Status == input.Status);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
		var items = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.CreationTime)
			.Skip((page - 1) * pageSize)
			.Take(pageSize));

		return new PaginatedResponse<LibraryNotificationHistoryDto>
		{
			Items = items.Select(MapHistory).ToList(),
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Notification.Add)]
	public async Task<LibraryNotificationSendResultDto> SendAsync(SendLibraryNotificationRequest input)
	{
		var definition = catalog.Resolve(input.EventType);
		if (!LibraryNotificationSource.IsConsistent(definition.EventType, input.RelatedEntityType, input.RelatedEntityId))
			throw new AbpAuthorizationException();
		var tenantWide = await libraryAccess.HasTenantWideScopeAsync();
		var recordedSource = LibraryNotificationCandidateScope.HasRecordedSource(input.RelatedEntityType);
		foreach (var recipient in input.Recipients)
		{
			if (recipient.MemberId.HasValue)
				recipient.MemberId = recordedSource
					? await candidates.RequireManualRecipientAsync(input.RelatedEntityType!, input.RelatedEntityId, recipient.MemberId.Value)
					: await libraryAccess.EnsureMemberAsync(recipient.MemberId.Value);
			else if (recordedSource || !tenantWide) throw new AbpAuthorizationException();
		}
		var histories = await reminderService.SendManualAsync(input);
		return BuildResult(histories);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Notification.Add)]
	public async Task<LibraryNotificationHistoryDto> RetryAsync(Guid historyId, RetryLibraryNotificationRequest input)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var history = await AsyncExecuter.FirstOrDefaultAsync((await GetScopedHistoryQueryAsync()).Where(x => x.Id == historyId))
			?? throw new EntityNotFoundException(typeof(LibraryNotificationHistory), historyId);
		if (history.Channel != LibraryNotificationChannels.Email ||
			history.Status is not (LibraryNotificationDeliveryStatuses.Failed or LibraryNotificationDeliveryStatuses.Unconfirmed))
			throw new BusinessException(LibraryNotificationErrorCodes.RetryNotAllowed);
		authority.Capture(history, authority.ForCurrentUser(ShikshaMitraSubmodulePermissions.Library.Notification.Add));
		if (!await authority.CanDispatchAsync(history)) throw new AbpAuthorizationException();
		history.QueueForDelivery(Clock.Now.ToUniversalTime(), input.AcknowledgePossibleDuplicate);
		await historyRepository.UpdateAsync(history, autoSave: true);
		await jobs.EnqueueAsync(new LibraryNotificationDeliveryJobArgs { TenantId = CurrentTenant.Id, HistoryId = history.Id });
		return MapHistory(history);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Notification.Add)]
	public async Task<LibraryNotificationRunRemindersResultDto> RunRemindersAsync(LibraryNotificationRunRemindersRequest? input = null)
	{
		var runDate = (input?.RunDate ?? await preferences.GetSchoolDateAsync()).Date;
		var allowedMembers = await libraryAccess.HasTenantWideScopeAsync() ? null : await libraryAccess.GetMemberIdsAsync();
		var histories = await reminderService.ProcessDueRemindersAsync(runDate, allowedMembers,
			authority.ForCurrentUser(ShikshaMitraSubmodulePermissions.Library.Notification.Add));
		var result = new LibraryNotificationRunRemindersResultDto
		{
			RunDate = runDate,
			Attempted = histories.Count,
			Queued = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Queued),
			Sent = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Sent),
			Skipped = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Skipped),
			Failed = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Failed),
			Items = histories.Select(MapHistory).ToList()
		};
		return result;
	}

	private async Task<Dictionary<string, LibraryNotificationHistory>> GetLatestHistoryBySettingKeyAsync()
	{
		var history = await AsyncExecuter.ToListAsync((await GetScopedHistoryQueryAsync())
			.OrderByDescending(x => x.CreationTime));

		return history
			.GroupBy(x => LibraryNotificationDeliveryService.BuildSettingKey(x.EventType, x.RecipientRole, x.Channel), StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
	}

	private async Task<IQueryable<LibraryNotificationHistory>> GetScopedHistoryQueryAsync()
	{
		var query = (await historyRepository.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id);
		if (!CurrentUser.Id.HasValue) return query.Where(_ => false);
		return await recordScope.ApplyAsync(query, await userScopes.GetUserScopeAsync(CurrentUser.Id.Value));
	}

	private static LibraryNotificationSetting? ResolveSetting(
		IReadOnlyCollection<LibraryNotificationSetting> settings,
		LibraryNotificationSettingUpdateDto update)
	{
		if (update.Id.HasValue)
		{
			return settings.FirstOrDefault(x => x.Id == update.Id.Value);
		}

		if (string.IsNullOrWhiteSpace(update.EventType) ||
		    string.IsNullOrWhiteSpace(update.RecipientRole) ||
		    string.IsNullOrWhiteSpace(update.Channel))
		{
			return null;
		}

		return settings.FirstOrDefault(x =>
			x.EventType.Equals(update.EventType, StringComparison.OrdinalIgnoreCase) &&
			x.RecipientRole.Equals(update.RecipientRole, StringComparison.OrdinalIgnoreCase) &&
			x.Channel.Equals(update.Channel, StringComparison.OrdinalIgnoreCase));
	}

	private static LibraryNotificationSettingDto MapSetting(
		LibraryNotificationSetting setting,
		LibraryNotificationHistory? latestHistory) =>
		new()
		{
			Id = setting.Id,
			EventType = setting.EventType,
			EventGroup = setting.EventGroup,
			EventName = setting.EventName,
			RecipientRole = setting.RecipientRole,
			Channel = setting.Channel,
			IsActive = setting.IsActive,
			IsChannelAvailable = setting.IsChannelAvailable,
			IsComingSoon = setting.IsComingSoon,
			ReminderOffsetDays = setting.ReminderOffsetDays,
			RepeatIntervalDays = setting.RepeatIntervalDays,
			SubjectTemplate = setting.SubjectTemplate,
			MessageTemplate = setting.MessageTemplate,
			LastStatus = latestHistory?.Status,
			LastError = latestHistory?.ErrorMessage,
			LastSentAt = latestHistory?.SentAt
		};

	private static LibraryNotificationHistoryDto MapHistory(LibraryNotificationHistory history) =>
		new()
		{
			Id = history.Id,
			EventType = history.EventType,
			EventGroup = history.EventGroup,
			EventName = history.EventName,
			Channel = history.Channel,
			RecipientRole = history.RecipientRole,
			RecipientMemberId = history.RecipientMemberId,
			RecipientUserId = history.RecipientUserId,
			RecipientKind = history.ExtraProperties.GetValueOrDefault("RecipientKind")?.ToString(),
			RecipientRelationship = history.ExtraProperties.GetValueOrDefault("RecipientRelationship")?.ToString(),
			ReadAt = history.ReadAt,
			RecipientName = history.RecipientName,
			RecipientEmail = history.RecipientEmail,
			Subject = history.Subject,
			Message = history.Message,
			Status = history.Status,
			ErrorMessage = history.ErrorMessage,
			RelatedEntityType = history.RelatedEntityType,
			RelatedEntityId = history.RelatedEntityId,
			ReminderDate = history.ReminderDate,
			SentAt = history.SentAt,
			AttemptCount = history.AttemptCount,
			NextAttemptAt = history.NextAttemptAt,
			CreationTime = history.CreationTime
		};

	private static LibraryNotificationSendResultDto BuildResult(IReadOnlyCollection<LibraryNotificationHistory> histories) =>
		new()
		{
			Attempted = histories.Count,
			Queued = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Queued),
			Sent = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Sent),
			Skipped = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Skipped),
			Failed = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Failed),
			Items = histories.Select(MapHistory).ToList()
		};
}
