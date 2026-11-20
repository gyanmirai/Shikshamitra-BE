using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Charges.Editor;

public partial class LibraryChargeEditorLookupAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Add)]
	public async Task<PaginatedResponse<LibraryChargeEditorLoanSourceDto>> GetLoanSourcesAsync(Guid academicYearId,
		Guid memberId, int page = 1, int pageSize = 20, string? search = null)
	{
		var context = await RequireAsync(ShikshaMitraSubmodulePermissions.Library.Charges.Add, academicYearId);
		var member = await members.ResolveAsync(context, memberId.ToString("D"));
		var aliases = (await memberships.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && row.StudentId == member.Id);
		// The assessment year and the original loan year are independent authorized
		// contexts. The stored-loan query validates the original context separately.
		var query = (await storedLoans.GetReadableLoansAsync(context.UserScope)).Where(loan =>
			loan.MemberRole == member.Role && (loan.MemberId == member.Id || member.Role == 0 && aliases.Any(alias => alias.Id == loan.MemberId)));
		var copyQuery = (await copies.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id);
		var bookQuery = (await books.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id);
		var term = SearchTerm(search);
		if (term.Length > 0) query = query.Where(loan => loan.AccessionNumber.ToLower().Contains(term) ||
			copyQuery.Any(copy => copy.Id == loan.BookCopyId && bookQuery.Any(book => book.Id == copy.BookId && book.Title.ToLower().Contains(term))));
		return await PageAsync(query.OrderByDescending(loan => loan.CreationTime).ThenBy(loan => loan.Id).Select(loan => new LibraryChargeEditorLoanSourceDto
		{
			LoanId = loan.Id, CanonicalMemberId = member.Id, Role = loan.MemberRole,
			CopyId = copyQuery.Where(copy => copy.Id == loan.BookCopyId).Select(copy => (Guid?)copy.Id).FirstOrDefault(),
			BookId = copyQuery.Where(copy => copy.Id == loan.BookCopyId).SelectMany(copy => bookQuery.Where(book => book.Id == copy.BookId))
				.Select(book => (Guid?)book.Id).FirstOrDefault(),
			AccessionNumber = loan.AccessionNumber,
			Title = copyQuery.Where(copy => copy.Id == loan.BookCopyId).SelectMany(copy => bookQuery.Where(book => book.Id == copy.BookId))
				.Select(book => book.Title).FirstOrDefault(),
			LoanAcademicYearId = loan.AcademicYearId, Status = (int)loan.Status,
			SchoolIssueDate = loan.SchoolIssueDate, SchoolDueDate = loan.SchoolDueDate,
			IssueDateProvenance = loan.IssueDateProvenance, DueDateProvenance = loan.DueDateProvenance
		}), page, pageSize);
	}
}
