using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Notifications;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Authorize]
[Area("Library")]
[RemoteService(Name = "Default")]
[Route("api/library/inbox")]
public class LibraryInboxController(ILibraryInboxAppService inbox) : AbpControllerBase
{
	[HttpGet]
	public Task<LibraryInboxResultDto> GetAsync([FromQuery] GetLibraryInboxRequest input) => inbox.GetAsync(input);

	[HttpPost("{id:guid}/read")]
	public Task<LibraryInboxItemDto> MarkReadAsync(Guid id) => inbox.MarkReadAsync(id);
}
