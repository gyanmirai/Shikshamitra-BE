using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Notifications;

public interface ILibraryNotificationAppService : IApplicationService
{
	Task<LibraryNotificationSettingsDto> GetSettingsAsync();
	Task<List<LibraryOperationalRecipientOptionDto>> GetOperationalRecipientsAsync(string? filter = null);
	Task<LibraryNotificationSettingsDto> UpdateSettingsAsync(UpdateLibraryNotificationSettingsRequest input);
	Task<PaginatedResponse<LibraryNotificationHistoryDto>> GetHistoryAsync(LibraryNotificationHistoryRequest input);
	Task<LibraryNotificationSendResultDto> SendAsync(SendLibraryNotificationRequest input);
	Task<LibraryNotificationHistoryDto> RetryAsync(Guid historyId, RetryLibraryNotificationRequest input);
	Task<LibraryNotificationRunRemindersResultDto> RunRemindersAsync(LibraryNotificationRunRemindersRequest? input = null);
}
