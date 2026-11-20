using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryLoanAppService
{
	private async Task<LibraryLoanCommandResultDto> ReviewDatesCoreAsync(Guid loanId, LibraryLoanDateReviewRequest input)
	{
		var actor = await BeginCommandAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.ReviewDates, input.IdempotencyKey);
		var fingerprint = PayloadHash(new { LoanId = loanId, Request = input });
		var replay = await ReplayAsync(actor, LibraryLoanOperation.ReviewDates, input.IdempotencyKey, fingerprint);
		if (replay != null) return replay;
		var loan = await RequireReadableLoanAsync(loanId);
		var copy = await copies.FindAsync(loan.BookCopyId);
		if (!string.Equals(input.Revision, await LoanRevisionAsync(loan, copy), StringComparison.Ordinal))
			throw new BusinessException(LibraryCirculationErrorCodes.PlanChanged, "The loan changed. Review its current dates before saving.");
		if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
			throw new BusinessException(LibraryCirculationErrorCodes.DateReviewRequired, "Record why these school dates are confirmed.");
		var before = JsonSerializer.Serialize(loan);
		loan.ReviewSchoolDates(input.SchoolIssueDate, input.SchoolDueDate, input.OriginalSchoolDueDate, input.SchoolReturnDate);
		await loans.UpdateAsync(loan, true);
		await RecordLoanEventAsync(loan, actor, LibraryLoanEventKind.DatesReviewed, input.Reason, before);
		return await SaveResultAsync(actor, LibraryLoanOperation.ReviewDates, input.IdempotencyKey, fingerprint, loan);
	}
}
