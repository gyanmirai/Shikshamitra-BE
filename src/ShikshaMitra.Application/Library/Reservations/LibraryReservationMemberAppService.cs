using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;

namespace ShikshaMitra.Library.Reservations;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
public class LibraryReservationMemberAppService(LibraryReservationMemberReader reader, IPermissionChecker permissions) : ApplicationService, ILibraryReservationMemberAppService
{
	public async Task<PaginatedResponse<LibraryReservationMemberDto>> SearchAsync(Guid academicYearId, string? search = null, int page = 1, int pageSize = 20)
	{
		await EnsurePermissionAsync();
		return await reader.SearchAsync(academicYearId, search, page, pageSize);
	}
	public async Task<LibraryReservationMemberDto> ResolveAsync(Guid academicYearId, string identifier)
	{
		await EnsurePermissionAsync();
		return await reader.ResolveAsync(academicYearId, identifier);
	}
	private async Task EnsurePermissionAsync()
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View) ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve)) throw new AbpAuthorizationException();
	}
}
