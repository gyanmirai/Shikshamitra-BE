using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/circulation/members")]
public class LibraryCirculationMemberController(ILibraryCirculationMemberAppService members) : AbpController
{
	[HttpGet]
	public Task<PaginatedResponse<LibraryCirculationMemberDto>> SearchAsync([FromQuery] string? search = null,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) => members.SearchAsync(search, page, pageSize);
	[HttpGet("resolve")]
	public Task<LibraryCirculationMemberDto> ResolveAsync([FromQuery] string identifier) => members.ResolveAsync(identifier);
}
