using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Catalogue;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
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
public partial class LibraryCirculationLookupAppService(LibraryCopyScanReader scans, IRepository<Book, Guid> books,
	IRepository<BookIssue, Guid> loans, IRepository<BookCategory, Guid> categories, ILibraryLoanAppService canonicalLoans,
	ILibraryAccessService access, LibraryStoredLoanScope storedScope, LibraryReservationMemberReader memberLabels,
	IPermissionChecker permissions, LibrarySchoolContextReader schoolContexts,
	IRepository<LibraryChargeCategory, Guid> assessmentCategories, LibraryCatalogueLabelReader catalogueLabels) : ApplicationService, ILibraryCirculationLookupAppService
{
	public async Task<LibraryCopyLookupDto> GetCopyAsync(string scan)
	{
		await EnsurePermissionAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue);
		var context = await access.GetContextAsync();
		var copy = await FindCopyAsync(scan) ?? throw new BusinessException(LibraryErrorCodes.CopyNotFound);
		var result = await MapCopyAsync(copy);
		result.SchoolContext = await ReadSchoolContextAsync(context);
		return result;
	}

	public async Task<LibraryReturnLookupDto> GetReturnAsync(string scan)
	{
		await EnsurePermissionAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return);
		var context = await access.GetContextAsync();
		var copy = await FindCopyAsync(scan);
		var normalized = (copy?.AccessionNumber ?? Normalize(scan)).ToUpperInvariant();
		var copyId = copy?.Id ?? LibraryCopyScanCode.ReadCopyId(Normalize(scan)) ?? Guid.Empty;
		var matches = await AsyncExecuter.ToListAsync((await loans.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			row.Status == BookIssueStatus.Issued && (copyId != Guid.Empty && row.BookCopyId == copyId ||
				row.AccessionNumber.Trim().ToUpper() == normalized)).Take(2));
		if (matches.Count == 0) throw new BusinessException(LibraryErrorCodes.ActiveIssueNotFound);
		var ids = matches.Select(row => row.Id).ToArray();
		if (!await AsyncExecuter.AnyAsync((await storedScope.GetReadableLoansAsync(context.UserScope)).Where(row => ids.Contains(row.Id))))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		if (matches.Count > 1) throw new BusinessException(LibraryErrorCodes.DuplicateActiveIssue,
			"This scan matches conflicting open loan records. Review their physical copy references before returning an item.");
		var loan = matches[0];
		if (copy == null || loan.BookCopyId != copy.Id)
			throw new BusinessException(LibraryErrorCodes.CopyLoanReconciliationRequired,
				"Review and reconcile this loan's physical copy reference. An accession match alone does not identify the physical copy.");
		var copyDetails = await MapCopyAsync(copy);
		var canonical = await canonicalLoans.GetAsync(loan.Id);
		// Lookup context already has its established outer/copy locations; keep the nested loan shape unchanged.
		canonical.SchoolContext = null;
		canonical.CanReviewDates = null;
		var borrower = LibraryLoanBorrowerSnapshot.Read(loan, canonical.CanonicalMemberId);
		if (borrower == null)
		{
			var personId = canonical.CanonicalMemberId ?? loan.MemberId;
			var current = (await memberLabels.ReadLabelsAsync([personId])).GetValueOrDefault(personId);
			borrower = current?.Role == loan.MemberRole
				? new() { MemberId = current.Id, MemberName = current.MemberName, MemberIdentifier = current.MemberIdentifier,
					Role = loan.MemberRole, ClassDivision = current.ClassDivision, DisplaySource = "CurrentAuthorizedRecord" }
				: new() { MemberId = canonical.CanonicalMemberId ?? loan.MemberId, Role = loan.MemberRole };
		}
		var schoolContext = await ReadSchoolContextAsync(context);
		copyDetails.SchoolContext = schoolContext;
		return new() { Copy = copyDetails, Loan = canonical, Borrower = borrower, SchoolContext = schoolContext };
	}

	private Task<BookCopy?> FindCopyAsync(string scan) => scans.FindAsync(CurrentTenant.Id, Normalize(scan));

	private async Task<LibraryCopyLookupDto> MapCopyAsync(BookCopy copy)
	{
		var book = await books.FindAsync(row => row.Id == copy.BookId && row.TenantId == CurrentTenant.Id)
			?? throw new BusinessException(LibraryErrorCodes.CopyNotFound);
		var category = await categories.FindAsync(row => row.Id == book.CategoryId && row.TenantId == CurrentTenant.Id);
		var labels = await catalogueLabels.ReadAsync();
		return new() { CopyId = copy.Id, CopyStatus = (int)copy.Status, BookId = book.Id, AccessionNo = copy.AccessionNumber,
			Title = book.Title, Author = book.Author, Isbn = book.Isbn, Publisher = book.Publisher, Edition = book.Edition,
			AddedOn = book.CreationTime, Language = labels.LanguageLabel(book) ?? string.Empty,
			LanguageCode = labels.LanguageCode(book), MaterialCode = book.MaterialCode, MaterialLabel = labels.MaterialLabel(book), Category = category?.Name ?? string.Empty,
			Pages = book.Pages, CoverImage = book.CoverImage };
	}

	private async Task EnsurePermissionAsync(string action)
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View) ||
			!await permissions.IsGrantedAsync(action)) throw new AbpAuthorizationException();
	}
	private static string Normalize(string scan) => !string.IsNullOrWhiteSpace(scan) && scan.Trim().Length <= 128
		? scan.Trim() : throw new BusinessException(LibraryErrorCodes.CopyNotFound);
}
