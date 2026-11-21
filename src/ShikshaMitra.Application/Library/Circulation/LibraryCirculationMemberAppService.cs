using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Circulation;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
public class LibraryCirculationMemberAppService(LibraryCirculationMemberReader reader)
	: ApplicationService, ILibraryCirculationMemberAppService
{
	public Task<LibraryCirculationMemberDto> ResolveAsync(string identifier) => reader.ResolveAsync(identifier);
	public Task<PaginatedResponse<LibraryCirculationMemberDto>> SearchAsync(string? search = null, int page = 1, int pageSize = 20) =>
		reader.SearchAsync(search, page, pageSize);
}
