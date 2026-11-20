using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Portal;

namespace ShikshaMitra.Library.Portal;

public partial class LibraryPortalAppService
{
	public async Task<PaginatedResponse<LibraryPortalBook>> SearchAsync(Guid studentId, string? search, int page = 1, int pageSize = 20)
	{
		var owner = await authority.ResolveAsync(studentId, Actions.View);
		(page, pageSize) = Page(page, pageSize);
		var term = search?.Trim().ToUpperInvariant();
		if (term?.Length > 100) throw new Volo.Abp.BusinessException(LibraryErrorCodes.ContextInvalid);
		var catalogue = (await books.GetQueryableAsync()).Where(row => row.TenantId == owner.Tenant);
		if (!string.IsNullOrEmpty(term))
			catalogue = catalogue.Where(row => row.Title.ToUpper().Contains(term) || (row.Author ?? "").ToUpper().Contains(term) ||
				(row.Isbn ?? "").ToUpper().Contains(term));
		var count = await AsyncExecuter.CountAsync(catalogue);
		var copyRows = await copies.GetQueryableAsync();
		var open = (await loans.GetQueryableAsync()).Where(row => row.TenantId == owner.Tenant && row.Status == BookIssueStatus.Issued);
		var items = await AsyncExecuter.ToListAsync(catalogue.OrderBy(row => row.Title).ThenBy(row => row.Id)
			.Skip((page - 1) * pageSize).Take(pageSize).Select(row => new LibraryPortalBook(row.Id, row.Title, row.Author, row.Isbn,
				copyRows.Count(copy => copy.TenantId == owner.Tenant && copy.BookId == row.Id && copy.Status == BookCopyStatus.Available &&
					!open.Any(loan => loan.BookCopyId == copy.Id || loan.AccessionNumber.Trim().ToUpper() == copy.NormalizedAccessionNumber)))));
		return Result(page, pageSize, count, items);
	}

	public async Task<PaginatedResponse<LibraryPortalLoan>> GetLoansAsync(Guid studentId, int page = 1, int pageSize = 20)
	{
		var owner = await authority.ResolveAsync(studentId, Actions.View);
		(page, pageSize) = Page(page, pageSize);
		var query = (await loans.GetQueryableAsync()).Where(row => row.TenantId == owner.Tenant && row.MemberRole == 0 &&
			Enumerable.Contains(owner.Aliases, row.MemberId));
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(row => row.Status != BookIssueStatus.Issued)
			.ThenByDescending(row => row.CreationTime).ThenBy(row => row.Id).Skip((page - 1) * pageSize).Take(pageSize));
		var ids = rows.Select(row => row.BookCopyId).ToArray();
		var physical = await copies.GetQueryableAsync();
		var catalogue = await books.GetQueryableAsync();
		var labels = await AsyncExecuter.ToListAsync(from copy in physical
			join book in catalogue on copy.BookId equals book.Id
			where copy.TenantId == owner.Tenant && book.TenantId == owner.Tenant && Enumerable.Contains(ids, copy.Id)
			select new { copy.Id, book.Title });
		var titles = labels.ToDictionary(row => row.Id, row => row.Title);
		var canRenew = await permissions.IsGrantedAsync(Actions.Renew) && owner.Facts.Eligibility == LibraryReservationEligibility.Eligible;
		return Result(page, pageSize, count, rows.Select(row => MapLoan(row, titles.GetValueOrDefault(row.BookCopyId), canRenew)).ToList());
	}

	public async Task<PaginatedResponse<LibraryPortalReservation>> GetReservationsAsync(Guid studentId, int page = 1, int pageSize = 20)
	{
		var owner = await authority.ResolveAsync(studentId, Actions.View);
		(page, pageSize) = Page(page, pageSize);
		var query = await OwnedRequestsAsync(owner);
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(row => row.CreationTime).ThenBy(row => row.Id)
			.Skip((page - 1) * pageSize).Take(pageSize));
		return Result(page, pageSize, count, await MapReservationsAsync(owner.Tenant, rows, await permissions.IsGrantedAsync(Actions.Reserve)));
	}

	private async Task<List<LibraryPortalReservation>> MapReservationsAsync(Guid tenant, List<BookReservation> rows, bool canCancel)
	{
		var booksInPage = rows.Select(row => row.BookId).Distinct().ToArray();
		var requestIds = rows.Where(row => row.State == LibraryReservationState.Ready).Select(row => row.Id).ToArray();
		var titles = (await AsyncExecuter.ToListAsync((await books.GetQueryableAsync()).Where(row =>
			row.TenantId == tenant && Enumerable.Contains(booksInPage, row.Id)).Select(row => new { row.Id, row.Title })))
			.ToDictionary(row => row.Id, row => row.Title);
		var allocation = (await holds.GetListAsync(row => row.TenantId == tenant && row.State == LibraryHoldState.Allocated &&
			Enumerable.Contains(requestIds, row.ReservationId))).ToLookup(row => row.ReservationId);
		return rows.Select(row =>
		{
			var holdsForRequest = allocation[row.Id].Take(2).ToArray();
			var hold = holdsForRequest.Length == 1 ? holdsForRequest[0] : null;
			return new LibraryPortalReservation(row.Id, titles.GetValueOrDefault(row.BookId), (int)row.State,
				Confirmed(row.SchoolRequestedDate, row.RequestedDateProvenance),
				hold == null ? null : Confirmed(hold.SchoolPickupDeadline, hold.PickupDeadlineProvenance),
				row.PickupLabel, row.Revision, canCancel &&
					row.State is LibraryReservationState.Waiting or LibraryReservationState.Ready or LibraryReservationState.NeedsReview);
		}).ToList();
	}

	private static LibraryPortalLoan MapLoan(BookIssue loan, string? title, bool canRenew)
	{
		var active = loan.Status == BookIssueStatus.Issued;
		return new(loan.Id, title, loan.AccessionNumber, LibraryLoanDates.ConfirmedIssueDate(loan),
			LibraryLoanDates.ConfirmedDueDate(loan), LibraryLoanDates.ConfirmedReturnDate(loan), active, loan.RenewalCount,
			canRenew && active && loan.PolicySnapshotJson != null && LibraryLoanDates.ConfirmedDueDate(loan).HasValue);
	}

	private static PaginatedResponse<T> Result<T>(int page, int size, int count, List<T> rows) => new()
	{
		CurrentPage = page, PageSize = size, TotalCount = count, TotalPages = (int)Math.Ceiling((double)count / size), Items = rows
	};
}
