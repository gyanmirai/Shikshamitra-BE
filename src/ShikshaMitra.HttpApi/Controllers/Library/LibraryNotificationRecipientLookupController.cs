using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/notifications/recipients")]
public class LibraryNotificationRecipientLookupController(ILibraryNotificationRecipientLookupAppService recipients) : AbpControllerBase
{
	[HttpGet]
	[ProducesResponseType(typeof(PaginatedResponse<LibraryNotificationRecipientOptionDto>), 200)]
	public Task<PaginatedResponse<LibraryNotificationRecipientOptionDto>> SearchAsync([FromQuery] LibraryNotificationRecipientSearchRequest input) =>
		recipients.SearchAsync(input);
}
