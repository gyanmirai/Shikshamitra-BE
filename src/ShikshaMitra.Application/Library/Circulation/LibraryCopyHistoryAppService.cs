using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Circulation;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
public class LibraryCopyHistoryAppService(IRepository<BookCopy, Guid> copies, IRepository<Book, Guid> books,
	LibraryCirculationReadService queries, ILibraryAccessService access, IPermissionChecker permissions)
	: ApplicationService, ILibraryCopyHistoryAppService
{
	public async Task<PaginatedResponse<IssueReturnItemDto>> GetAsync(Guid bookId, Guid copyId, int page = 1, int pageSize = 20,
		Guid? academicYearId = null, DateOnly? asOfDate = null)
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)) throw new AbpAuthorizationException();
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		await access.GetContextAsync(academicYearId);
		var copy = await copies.FindAsync(row => row.Id == copyId && row.TenantId == CurrentTenant.Id);
		if (copy == null || copy.BookId != bookId ||
			!await books.AnyAsync(row => row.Id == bookId && row.TenantId == CurrentTenant.Id)) throw new BusinessException(LibraryErrorCodes.CopyNotFound);
		return await queries.GetPageAsync(copyId, page, pageSize, academicYearId, asOfDate);
	}
}
