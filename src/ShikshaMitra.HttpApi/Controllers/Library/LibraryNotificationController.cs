using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("Library")]
[RemoteService(Name = "Default")]
[Route("api/library/notifications")]
public class LibraryNotificationController(ILibraryNotificationAppService notificationAppService) : AbpControllerBase
{
	[HttpGet("settings")]
	[ProducesResponseType(typeof(LibraryNotificationSettingsDto), StatusCodes.Status200OK)]
	public Task<LibraryNotificationSettingsDto> GetSettingsAsync()
	{
		return notificationAppService.GetSettingsAsync();
	}

	[HttpPut("settings")]
	[ProducesResponseType(typeof(LibraryNotificationSettingsDto), StatusCodes.Status200OK)]
	public Task<LibraryNotificationSettingsDto> UpdateSettingsAsync([FromBody] UpdateLibraryNotificationSettingsRequest input)
	{
		return notificationAppService.UpdateSettingsAsync(input);
	}

	[HttpGet("history")]
	[ProducesResponseType(typeof(PaginatedResponse<LibraryNotificationHistoryDto>), StatusCodes.Status200OK)]
	public Task<PaginatedResponse<LibraryNotificationHistoryDto>> GetHistoryAsync([FromQuery] LibraryNotificationHistoryRequest input)
	{
		return notificationAppService.GetHistoryAsync(input);
	}

	[HttpPost("send")]
	[ProducesResponseType(typeof(LibraryNotificationSendResultDto), StatusCodes.Status200OK)]
	public Task<LibraryNotificationSendResultDto> SendAsync([FromBody] SendLibraryNotificationRequest input)
	{
		return notificationAppService.SendAsync(input);
	}

	[HttpPost("run-reminders")]
	[ProducesResponseType(typeof(LibraryNotificationRunRemindersResultDto), StatusCodes.Status200OK)]
	public Task<LibraryNotificationRunRemindersResultDto> RunRemindersAsync([FromBody] LibraryNotificationRunRemindersRequest? input = null)
	{
		return notificationAppService.RunRemindersAsync(input);
	}
}
