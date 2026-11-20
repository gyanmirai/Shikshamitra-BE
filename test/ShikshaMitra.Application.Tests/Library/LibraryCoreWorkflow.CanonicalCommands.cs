using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library;

public partial class LibraryCoreWorkflow_Tests
{
	private async Task<GenericCreatedResponse> IssueCanonicalAsync(Guid copyId, Guid memberId, DateOnly issueDate, DateOnly dueDate)
	{
		var plan = await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = copyId, MemberId = memberId, IssueDate = issueDate, RequestedDueDate = dueDate
		});
		return await _bookIssueAppService.IssueAsync(new() { Checkout = new()
		{
			CopyId = copyId, MemberId = memberId, AcademicYearId = plan.AcademicYearId, IssueDate = issueDate,
			RequestedDueDate = dueDate, Revision = plan.Revision, IdempotencyKey = Guid.NewGuid().ToString("N")
		} });
	}

	private async Task<GenericCreatedResponse> ReturnCanonicalAsync(Guid loanId, Guid copyId, LibraryTestContext context,
		int copyStatus = 0, decimal? amount = null, string? remarks = null)
	{
		var loan = await GetRequiredService<ILibraryLoanAppService>().GetAsync(loanId);
		return await _bookIssueAppService.ReturnAsync(new() { LoanId = loanId, Return = new()
		{
			CopyId = copyId, Revision = loan.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"), SchoolReturnDate = new(2026, 11, 17),
			CopyStatus = copyStatus, Remarks = remarks, UnknownOriginalDatesReason = "Retained the seeded legacy timestamp uncertainty during physical receipt.",
			Assessments = amount.HasValue && amount != 0 ? [new()
			{
				ChargeCategoryId = context.ChargeCategoryId, Amount = amount.Value, AcademicYearId = context.AcademicYearId,
				AcademicYearReason = "This is the new inspection assessment's selected school year.", SchoolChargeDate = new(2026, 11, 17),
				SchoolDueDate = new(2026, 11, 22), Reason = remarks ?? "New inspection assessment."
			}] : []
		} });
	}
}
