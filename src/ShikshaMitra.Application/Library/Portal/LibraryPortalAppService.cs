using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Mobile;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Circulation;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students.Portal;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Portal;

namespace ShikshaMitra.Library.Portal;

[RemoteService(false)]
[Authorize(Actions.View)]
public partial class LibraryPortalAppService(LibraryPortalAuthority authority, StudentPortalAccessService portal,
	IPermissionChecker permissions, IRepository<Book, Guid> books, IRepository<BookCopy, Guid> copies,
	IRepository<BookIssue, Guid> loans, IRepository<BookReservation, Guid> requests, IRepository<LibraryHold, Guid> holds,
	LibraryCalendarService calendar, LibraryReservationPickupReader pickup, LibraryReservationCoordinator reservations,
	LibraryNotificationReminderService notifications, ILibraryInventoryLock inventoryLock, LibraryLoanAppService loanCommands,
	MobileLibraryReceiptReader financeReceipts)
	: ApplicationService, ILibraryPortalAppService
{
	public async Task<List<LibraryPortalStudent>> GetStudentsAsync()
	{
		await authority.RequireSchoolAsync(Actions.View);
		var allowed = await portal.GetMyStudentsAsync();
		return allowed.Select(row => new LibraryPortalStudent(row.StudentId, row.StudentName)).DistinctBy(row => row.StudentId)
			.OrderBy(row => row.Name).ThenBy(row => row.StudentId).ToList();
	}

	[Authorize(MobileFinancePermissions.ViewReceipts)]
	public async Task<List<LibraryPortalReceiptYear>> GetReceiptYearsAsync(Guid studentId)
	{
		await authority.ResolveAsync(studentId, Actions.View);
		return await financeReceipts.ListYearsAsync(studentId);
	}

	[Authorize(MobileFinancePermissions.ViewReceipts)]
	public async Task<PaginatedResponse<LibraryPortalReceiptDto>> GetReceiptsAsync(Guid studentId, Guid academicYearId, int page = 1, int pageSize = 20)
	{
		await authority.ResolveAsync(studentId, Actions.View);
		(page, pageSize) = Page(page, pageSize);
		return await financeReceipts.ListAsync(studentId, academicYearId, page, pageSize);
	}

	private async Task<IQueryable<BookReservation>> OwnedRequestsAsync(LibraryPortalOwner owner) =>
		(await requests.GetQueryableAsync()).Where(row => row.TenantId == owner.Tenant && (row.MemberRole == 0 || row.MemberRole == null) &&
			(row.CanonicalMemberId == owner.StudentId || !row.CanonicalMemberId.HasValue && Enumerable.Contains(owner.Aliases, row.MemberId)));

	private static (int Page, int Size) Page(int page, int size)
	{
		size = Math.Clamp(size, 1, 50);
		return (Math.Clamp(page, 1, int.MaxValue / size), size);
	}

	private static DateOnly? Confirmed(DateOnly? date, LibraryCivilDateProvenance provenance) =>
		provenance is LibraryCivilDateProvenance.ExplicitSchoolDate or LibraryCivilDateProvenance.ReviewedLegacy ? date : null;
}
