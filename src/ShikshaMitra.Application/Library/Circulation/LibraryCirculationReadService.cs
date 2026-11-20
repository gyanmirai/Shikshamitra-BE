using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Circulation;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
[ExposeServices(typeof(ILibraryCirculationReadAppService), typeof(LibraryCirculationReadService))]
public partial class LibraryCirculationReadService(ILibraryAccessService access, LibraryStoredLoanScope storedScope,
	IRepository<BookCopy, Guid> copies, IRepository<Book, Guid> books,
	IRepository<BookCategory, Guid> categories, IRepository<LibraryMember, Guid> memberships,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IPermissionChecker permissions, LibraryCalendarService calendar, IClock clock)
	: ApplicationService, ITransientDependency, ILibraryCirculationReadAppService
{
	public Task<PaginatedResponse<IssueReturnItemDto>> GetListAsync(int page = 1, int pageSize = 20,
		Guid? academicYearId = null, DateOnly? asOfDate = null) => GetPageAsync(null, page, pageSize, academicYearId, asOfDate);

	public async Task<PaginatedResponse<IssueReturnItemDto>> GetPageAsync(Guid? copyId, int page, int pageSize,
		Guid? academicYearId = null, DateOnly? asOfDate = null)
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)) throw new AbpAuthorizationException();
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		var context = await access.GetContextAsync(academicYearId);
		var query = await storedScope.GetReadableLoansAsync(context.UserScope);
		if (academicYearId.HasValue) query = query.Where(loan => loan.AcademicYearId == academicYearId.Value);
		if (copyId.HasValue) query = query.Where(loan => loan.BookCopyId == copyId.Value);
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 100);
		var count = await AsyncExecuter.CountAsync(query);
		var offset = ((long)page - 1) * pageSize;
		List<BookIssue> rows = offset >= count ? [] : await AsyncExecuter.ToListAsync(query
			.OrderByDescending(loan => loan.SchoolIssueDate ?? (loan.IssueDate.HasValue ? DateOnly.FromDateTime(loan.IssueDate.Value) : (DateOnly?)null))
			.ThenByDescending(loan => loan.CreationTime).ThenBy(loan => loan.Id).Skip((int)offset).Take(pageSize));
		return new()
		{
			TotalCount = count, CurrentPage = page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(count / (double)pageSize), Items = await MapCanonicalAsync(rows, await ReadAsOfAsync(asOfDate))
		};
	}
}
