using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Books;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
public partial class LibraryCopyLifecycleAppService(IRepository<Book, Guid> books, IRepository<BookCopy, Guid> copies,
	IRepository<BookIssue, Guid> loans, IRepository<LibraryCopyEvent, Guid> events,
	IRepository<LibrarySection, Guid> sections, IRepository<LibraryShelf, Guid> shelves,
	ILibraryAccessService access, LibraryStoredLoanScope storedLoans, ILibraryInventoryLock inventoryLock, LibraryCopyEventRecorder recorder,
	IPermissionChecker permissions, LibraryReservationCoordinator reservationCoordinator) : ApplicationService, ILibraryCopyLifecycleAppService
{
	public async Task<LibraryCopyStateDto> GetStateAsync(Guid bookId, Guid copyId) => await StateAsync(await LoadAsync(bookId, copyId));

	[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update)]
	public async Task<LibraryCopyMoveOptionsDto> GetMoveOptionsAsync(Guid bookId, Guid copyId, Guid? academicYearId = null)
	{
		await EnsurePermissionAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update);
		var context = await LoadAsync(bookId, copyId);
		if (academicYearId.HasValue) await access.GetContextAsync(academicYearId.Value);
		var result = new LibraryCopyMoveOptionsDto { State = await StateAsync(context) };
		if (!result.State.CanMove) return result;
		var source = await sections.FindAsync(section => section.TenantId == CurrentTenant.Id && section.Id == context.Copy.SectionId);
		var campusId = source?.CampusId;
		var destinations = await sections.GetListAsync(section => section.TenantId == CurrentTenant.Id && section.CampusId == campusId);
		var sectionIds = destinations.Select(section => section.Id).ToArray();
		var shelfRows = await AsyncExecuter.ToListAsync((await shelves.GetQueryableAsync())
			.Where(shelf => Enumerable.Contains(sectionIds, shelf.SectionId)));
		var bySection = shelfRows.GroupBy(shelf => shelf.SectionId).ToDictionary(group => group.Key, group => group.ToList());
		result.Sections = destinations.Where(section => bySection.ContainsKey(section.Id))
			.OrderBy(section => section.SectionName).ThenBy(section => section.Id).Select(section => new LibraryCopyMoveSectionDto
			{
				Id = section.Id, SectionName = section.SectionName ?? string.Empty,
				ShelfDetails = bySection[section.Id].OrderBy(shelf => shelf.ShelfName).ThenBy(shelf => shelf.Id)
					.Select(shelf => new LibraryCopyMoveShelfDto { Id = shelf.Id, ShelfName = shelf.ShelfName ?? string.Empty }).ToList()
			}).ToList();
		return result;
	}

	public async Task<PaginatedResponse<LibraryCopyEventDto>> GetEventsAsync(Guid bookId, Guid copyId, int page = 1, int pageSize = 20)
	{
		var context = await LoadAsync(bookId, copyId);
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 100);
		var query = (await events.GetQueryableAsync()).Where(row => row.BookId == bookId && row.CopyId == copyId);
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(row => row.Sequence).Skip((page - 1) * pageSize).Take(pageSize));
		var referenced = rows.Where(row => row.RelatedLoanId.HasValue).Select(row => row.RelatedLoanId!.Value).Distinct().ToArray();
		var visibleLoanIds = new HashSet<Guid>();
		if (referenced.Length > 0)
		{
			visibleLoanIds = (await AsyncExecuter.ToListAsync((await storedLoans.GetReadableLoansAsync(context.Academic.UserScope))
				.Where(loan => Enumerable.Contains(referenced, loan.Id)).Select(loan => loan.Id))).ToHashSet();
		}
		return new()
		{
			TotalCount = count, CurrentPage = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(count / (double)pageSize),
			Items = rows.Select(row => new LibraryCopyEventDto
			{
				Id = row.Id, Sequence = row.Sequence, BookId = row.BookId, CopyId = row.CopyId, EventType = row.EventType.ToString(),
				OccurredAt = row.OccurredAt, ActorKind = (int)row.ActorKind, ActorId = row.ActorId,
				SystemPurpose = (int?)row.SystemPurpose, Reason = row.Reason,
				PreviousStatus = (int)row.PreviousStatus, NextStatus = (int)row.NextStatus,
				PreviousRemarks = row.PreviousRemarks, NextRemarks = row.NextRemarks,
				FromSectionId = row.FromSectionId, FromShelfId = row.FromShelfId, ToSectionId = row.ToSectionId, ToShelfId = row.ToShelfId,
				RelatedLoanId = row.RelatedLoanId.HasValue && visibleLoanIds.Contains(row.RelatedLoanId.Value) ? row.RelatedLoanId : null
			}).ToList()
		};
	}

	private async Task<CopyContext> LoadAsync(Guid bookId, Guid copyId)
	{
		await books.GetAsync(bookId);
		var copy = await copies.FindAsync(row => row.Id == copyId && row.BookId == bookId)
			?? throw new EntityNotFoundException(typeof(BookCopy), copyId);
		var academic = await access.GetContextAsync();
		var accession = copy.AccessionNumber.Trim().ToUpperInvariant();
		var active = await AsyncExecuter.ToListAsync((await loans.GetQueryableAsync()).Where(loan => loan.Status == BookIssueStatus.Issued &&
			(loan.BookCopyId == copyId || loan.AccessionNumber.Trim().ToUpper() == accession)).Take(2));
		if (active.Count > 1) throw new BusinessException(LibraryErrorCodes.DuplicateActiveIssue, "This copy has conflicting open loan records. Review its circulation history.");
		var loan = active.SingleOrDefault();
		var readable = loan == null || await storedLoans.CanReadAsync(loan.Id, academic.UserScope);
		return new(copy, loan, academic, readable);
	}

	private async Task<LibraryCopyStateDto> StateAsync(CopyContext context)
	{
		var copy = context.Copy;
		var open = context.Loan != null && !context.RequiresReconciliation;
		var canMutate = context.ReadableLoan && !context.RequiresReconciliation;
		var update = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Update);
		var delete = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.Delete);
		var canReturn = open && context.ReadableLoan && await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return);
		var allowed = update && canMutate ? LibraryCopyConditionRules.AllowedConditions(copy.Status, open) : [];
		var result = new LibraryCopyStateDto
		{
			BookId = copy.BookId, CopyId = copy.Id, AccessionNumber = copy.AccessionNumber, Status = (int)copy.Status, Remarks = copy.Remarks,
			SectionId = copy.SectionId, ShelfId = copy.ShelfId, Revision = await recorder.RevisionAsync(copy, context.Loan?.Id),
			HasOpenLoan = open, ActiveLoanId = context.ReadableLoan && !context.RequiresReconciliation ? context.Loan?.Id : null, CanReturn = canReturn,
			LoanLinkSource = context.RequiresReconciliation ? "LegacyAccessionCandidate" : open ? "CopyId" : "None",
			RequiresLoanReconciliation = context.RequiresReconciliation,
			CanChangeCondition = allowed.Count > 0, AllowedConditionStatuses = allowed.Select(status => (int)status).ToList(),
			CanEditNotes = update && canMutate && Enum.IsDefined(copy.Status),
			CanMove = update && canMutate && LibraryCopyConditionRules.CanMove(copy.Status, open),
			CanWriteOff = delete && context.Academic.UserScope.HasTenantWideScope() && canMutate && LibraryCopyConditionRules.CanWriteOff(copy.Status, open)
		};
		if (!context.ReadableLoan) result.BlockedReasons.Add("You do not have access to this borrower's loan.");
		if (context.RequiresReconciliation) result.BlockedReasons.Add("A legacy loan matches this accession but references another copy. Review and reconcile the physical copy link before taking action.");
		if (open) result.BlockedReasons.Add("Use the return or recovery workflow before moving, repairing, making available, or writing off a copy with an open loan.");
		if (copy.Status == BookCopyStatus.Reserved) result.BlockedReasons.Add("Release the hold before changing this copy's condition or location.");
		if (copy.Status == BookCopyStatus.InCustody) result.BlockedReasons.Add("Receive the copy through Classroom Custody before using it again.");
		if (copy.Status == BookCopyStatus.InTransit) result.BlockedReasons.Add("Record the Campus Transfer receipt before using this copy again.");
		if (copy.Status == BookCopyStatus.Discarded) result.BlockedReasons.Add("This copy is written off. Its inventory and history are retained.");
		if (!update) result.BlockedReasons.Add("Changing condition or location requires catalogue update permission.");
		if (!delete) result.BlockedReasons.Add("Writing off requires catalogue delete permission.");
		if (!context.Academic.UserScope.HasTenantWideScope()) result.BlockedReasons.Add("Writing off requires authority over the whole school's inventory.");
		return result;
	}

	private static void EnsureLoanScope(CopyContext context)
	{
		if (context.RequiresReconciliation) throw new BusinessException(LibraryErrorCodes.CopyLoanReconciliationRequired,
			"Review and reconcile the loan's physical copy reference before changing this copy. An accession match alone does not confirm the copy.");
		if (!context.ReadableLoan) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	private async Task EnsurePermissionAsync(string permission)
	{
		if (!await permissions.IsGrantedAsync(permission)) throw new AbpAuthorizationException();
	}

	private async Task EnsureRevisionAsync(CopyContext context, string? revision, bool required = true)
	{
		if (!required && string.IsNullOrWhiteSpace(revision)) return;
		if (string.IsNullOrWhiteSpace(revision) || !string.Equals(revision, await recorder.RevisionAsync(context.Copy, context.Loan?.Id), StringComparison.Ordinal))
			throw new BusinessException(LibraryErrorCodes.CopyStateConflict, "This copy changed after you opened it. Reload its current state before saving.");
	}

	private sealed record CopyContext(BookCopy Copy, BookIssue? Loan, LibraryAcademicContext Academic, bool ReadableLoan)
	{
		public bool RequiresReconciliation => Loan != null && Loan.BookCopyId != Copy.Id;
	}
}
