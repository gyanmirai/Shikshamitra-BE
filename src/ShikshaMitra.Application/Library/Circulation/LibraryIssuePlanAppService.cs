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
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Circulation;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
public partial class LibraryIssuePlanAppService(ILibraryAccessService access, LibraryCirculationMemberReader borrowers,
	LibraryPolicyContextReader policyContexts, LibraryBorrowingPolicyResolver policies, LibraryCalendarEvidenceReader calendars,
	IRepository<Book, Guid> books, IRepository<BookCopy, Guid> copies, IRepository<BookIssue, Guid> loans,
	IRepository<LibraryMember, Guid> memberships, LibraryCopyEventRecorder copyEvents, IPermissionChecker permissions,
	LibraryReservationCoordinator reservations)
	: ApplicationService, ILibraryIssuePlanAppService
{
	public async Task<LibraryIssuePlanDto> PreviewAsync(LibraryIssuePlanRequest input)
	{
		await access.GetContextAsync();
		if (input.CopyId == Guid.Empty || input.MemberId == Guid.Empty || input.IssueDate == default)
			throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		var borrower = await borrowers.ResolveAsync(input.MemberId.ToString());
		var yearId = borrower.AcademicYearId ?? input.AcademicYearId;
		if (!yearId.HasValue || yearId == Guid.Empty)
			throw new BusinessException(LibraryErrorCodes.AcademicYearRequired, "Select the staff borrower's academic year for this loan.");
		if (input.AcademicYearId.HasValue && input.AcademicYearId != yearId)
			throw new BusinessException(LibraryErrorCodes.PolicyInvalid, "Use the borrower's current academic year for a new loan.");
		await access.GetContextAsync(yearId);
		var copy = await copies.FindAsync(input.CopyId);
		if (copy == null || copy.TenantId != CurrentTenant.Id) throw new BusinessException(LibraryErrorCodes.CopyNotFound);
		var book = await AsyncExecuter.FirstOrDefaultAsync((await books.GetQueryableAsync())
			.Where(x => x.Id == copy.BookId && x.TenantId == CurrentTenant.Id)
			.Select(x => new { x.Id, x.CategoryId, x.Title, x.Isbn, x.Language, x.Price, x.ConcurrencyStamp }));
		if (book == null) throw new BusinessException(LibraryErrorCodes.BookNotFound);
		var context = await policyContexts.ReadAsync(new()
		{
			AcademicYearId = yearId.Value, MemberId = borrower.Id, BookId = book.Id, CopyId = copy.Id, Role = borrower.Role
		}, input.IssueDate, borrower);
		var decision = await policies.ResolveAsync(context);
		var calendar = await calendars.ReadAsync(yearId.Value, copy.SectionId);
		var dueDate = ResolveDueDate(input, decision, calendar, new DateTimeOffset(Clock.Now.ToUniversalTime()));
		var aliases = (await memberships.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == borrower.Id).Select(x => x.Id);
		var active = await AsyncExecuter.ToListAsync((await loans.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.Status == BookIssueStatus.Issued &&
			(x.BookCopyId == copy.Id || x.AccessionNumber.Trim().ToUpper() == copy.NormalizedAccessionNumber ||
			 x.MemberId == borrower.Id || aliases.Contains(x.MemberId)))
			.OrderBy(x => x.Id).Select(x => new { x.Id, x.BookCopyId, x.AccessionNumber, x.MemberId, x.ConcurrencyStamp }));
		var aliasIds = await AsyncExecuter.ToListAsync(aliases);
		var count = active.Count(x => x.MemberId == borrower.Id || aliasIds.Contains(x.MemberId));
		var copyLoans = active.Where(x => x.BookCopyId == copy.Id ||
			x.AccessionNumber.Trim().ToUpperInvariant() == copy.NormalizedAccessionNumber).ToList();
		var priority = await reservations.ReadIssuePriorityAsync(copy, borrower.Id);
		var blocked = new List<string>();
		if (!priority.CanIssue) blocked.Add(priority.BlockedReason ?? "Reservation priority prevents this checkout.");
		if (!borrower.IsEligibleForIssue) blocked.Add(borrower.IneligibilityReason ?? "Library membership is not active.");
		if (copy.Status is not (BookCopyStatus.Available or BookCopyStatus.Reserved)) blocked.Add("This copy is not available for checkout.");
		if (copyLoans.Count > 0) blocked.Add("This physical copy already has an open loan.");
		if (count >= decision.MaxBooksAtTime) blocked.Add("This borrower has reached the active loan limit.");
		if (!dueDate.HasValue) blocked.Add("Enter the school's due date before checkout.");
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue))
			blocked.Add("You do not have permission to issue books.");
		var snapshot = ObjectMapper.Map<LibraryBorrowingPolicyDecision, LibraryBorrowingPolicySnapshotDto>(decision);
		snapshot.TimeZoneId = calendar.Settings.TimeZoneId;
		snapshot.TimeZoneSource = calendar.Settings.Source;
		snapshot.NeedsCalendarConfiguration = calendar.Settings.NeedsConfiguration;
		var result = new LibraryIssuePlanDto
		{
			BookId = book.Id, CopyId = copy.Id, MemberId = borrower.Id, Role = borrower.Role, AcademicYearId = yearId.Value,
			IssueDate = input.IssueDate, DueDate = dueDate, Policy = snapshot, ActiveLoanCount = count,
			HasOpenLoan = copyLoans.Count > 0, BlockedReasons = blocked, CanIssue = blocked.Count == 0,
			ConfirmedSchoolTimeZoneId = calendar.Settings.Source == "Library" && !calendar.Settings.NeedsConfiguration
				? calendar.Settings.TimeZoneId : null
		};
		result.Revision = Fingerprint(new
		{
			Version = 1, CurrentTenant.Id, ActorId = CurrentUser.Id, Result = result, Borrower = borrower, Context = context,
			Book = book, Calendar = calendar.Revision, ActiveLoans = active, ReservationPriority = priority.Revision,
			Copy = await copyEvents.RevisionAsync(copy, copyLoans.Count == 1 ? copyLoans[0].Id : null)
		});
		return result;
	}
}
