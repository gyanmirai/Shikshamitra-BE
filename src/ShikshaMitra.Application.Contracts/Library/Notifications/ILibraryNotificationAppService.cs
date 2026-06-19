using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Notifications;

public interface ILibraryNotificationAppService : IApplicationService
{
	Task<LibraryNotificationSettingsDto> GetSettingsAsync();
	Task<LibraryNotificationSettingsDto> UpdateSettingsAsync(UpdateLibraryNotificationSettingsRequest input);
	Task<PaginatedResponse<LibraryNotificationHistoryDto>> GetHistoryAsync(LibraryNotificationHistoryRequest input);
	Task<LibraryNotificationSendResultDto> SendAsync(SendLibraryNotificationRequest input);
	Task<LibraryNotificationRunRemindersResultDto> RunRemindersAsync(LibraryNotificationRunRemindersRequest? input = null);
}
