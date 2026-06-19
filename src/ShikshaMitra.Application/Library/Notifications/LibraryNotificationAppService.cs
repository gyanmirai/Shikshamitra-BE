using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Notifications;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Notification.View)]
[RemoteService(false)]
public class LibraryNotificationAppService(
	IRepository<LibraryNotificationSetting, Guid> settingRepository,
	IRepository<LibraryNotificationHistory, Guid> historyRepository,
	LibraryNotificationCatalog catalog,
	LibraryNotificationDeliveryService deliveryService,
	LibraryNotificationReminderService reminderService)
	: ApplicationService, ILibraryNotificationAppService
{
	public async Task<LibraryNotificationSettingsDto> GetSettingsAsync()
	{
		var settings = await deliveryService.EnsureSettingsAsync();
		var latestHistory = await GetLatestHistoryBySettingKeyAsync();

		return new LibraryNotificationSettingsDto
		{
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

		return await GetSettingsAsync();
	}

	public async Task<PaginatedResponse<LibraryNotificationHistoryDto>> GetHistoryAsync(LibraryNotificationHistoryRequest input)
	{
		var page = Math.Max(input.Page, 1);
		var pageSize = Math.Clamp(input.PageSize, 1, 100);
		var query = await historyRepository.GetQueryableAsync();

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
		catalog.Resolve(input.EventType);
		var histories = await reminderService.SendManualAsync(input);
		return BuildResult(histories);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Notification.Add)]
	public async Task<LibraryNotificationRunRemindersResultDto> RunRemindersAsync(LibraryNotificationRunRemindersRequest? input = null)
	{
		var runDate = (input?.RunDate ?? DateTime.UtcNow).Date;
		var histories = await reminderService.ProcessDueRemindersAsync(runDate);
		var result = new LibraryNotificationRunRemindersResultDto
		{
			RunDate = runDate,
			Attempted = histories.Count,
			Sent = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Sent),
			Skipped = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Skipped),
			Failed = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Failed),
			Items = histories.Select(MapHistory).ToList()
		};
		return result;
	}

	private async Task<Dictionary<string, LibraryNotificationHistory>> GetLatestHistoryBySettingKeyAsync()
	{
		var history = await AsyncExecuter.ToListAsync((await historyRepository.GetQueryableAsync())
			.OrderByDescending(x => x.CreationTime));

		return history
			.GroupBy(x => LibraryNotificationDeliveryService.BuildSettingKey(x.EventType, x.RecipientRole, x.Channel), StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
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
			CreationTime = history.CreationTime
		};

	private static LibraryNotificationSendResultDto BuildResult(IReadOnlyCollection<LibraryNotificationHistory> histories) =>
		new()
		{
			Attempted = histories.Count,
			Sent = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Sent),
			Skipped = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Skipped),
			Failed = histories.Count(x => x.Status == LibraryNotificationDeliveryStatuses.Failed),
			Items = histories.Select(MapHistory).ToList()
		};
}
