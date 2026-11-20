using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.IssueReturn;
using Volo.Abp;
using Volo.Abp.Uow;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Portal;

namespace ShikshaMitra.Library.Portal;

public partial class LibraryPortalAppService
{
	[Authorize(Actions.Renew)]
	public async Task<LibraryPortalRenewPreview> PreviewRenewAsync(Guid studentId, Guid loanId)
	{
		var owner = await authority.ResolveAsync(studentId, Actions.Renew);
		await loanCommands.RequirePortalLoanAsync(owner, loanId);
		var today = await ConfirmedSchoolDateAsync();
		if (!today.HasValue) return new(false, null, null, null, ["The library must confirm the school calendar before online renewal."]);
		var plan = await loanCommands.BuildRenewalPlanAsync(loanId,
			new() { AcademicYearId = owner.Facts.AcademicYearId, SchoolRenewalDate = today.Value }, owner);
		return new(plan.CanRenew, plan.NewSchoolDueDate, today, plan.Revision, plan.BlockedReasons);
	}

	[Authorize(Actions.Renew)]
	[UnitOfWork(true)]
	public async Task<LibraryPortalLoan> RenewAsync(Guid studentId, Guid loanId, LibraryPortalRenewInput input)
	{
		var tenant = await authority.RequireSchoolAsync(Actions.Renew);
		if (input.SchoolDate == default || input.Revision?.Length != 64 || string.IsNullOrWhiteSpace(input.IdempotencyKey) ||
			input.IdempotencyKey.Length > 100 || input.IdempotencyKey.Contains('\0') || input.IdempotencyKey.Trim() != input.IdempotencyKey)
			throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		await inventoryLock.AcquireAsync(tenant);
		var owner = await authority.ResolveAsync(studentId, Actions.Renew);
		var saved = await loanCommands.RenewCoreAsync(loanId, new LibraryLoanRenewRequest
		{
			AcademicYearId = owner.Facts.AcademicYearId, SchoolRenewalDate = input.SchoolDate, Revision = input.Revision,
			IdempotencyKey = input.IdempotencyKey, Reason = "Renewed through My Library."
		}, owner);
		var loan = await loanCommands.RequirePortalLoanAsync(owner, loanId);
		var title = saved.Loan.BookId is Guid bookId ? (await books.FindAsync(row => row.Id == bookId && row.TenantId == tenant))?.Title : null;
		return new(saved.Loan.Id, title, loan.AccessionNumber,
			Confirmed(saved.Loan.SchoolIssueDate, (Shared.LibraryCivilDateProvenance)saved.Loan.IssueDateProvenance),
			Confirmed(saved.Loan.SchoolDueDate, (Shared.LibraryCivilDateProvenance)saved.Loan.DueDateProvenance),
			Confirmed(saved.Loan.SchoolReturnDate, (Shared.LibraryCivilDateProvenance)saved.Loan.ReturnDateProvenance),
			saved.Loan.Status == (int)Books.BookIssueStatus.Issued, saved.Loan.RenewalCount, saved.Loan.CanRenew);
	}
}
