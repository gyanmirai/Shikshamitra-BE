using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Portal;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryLoanAppService
{
	internal async Task<LibraryLoanCommandResultDto> RenewCoreAsync(Guid loanId, LibraryLoanRenewRequest input, LibraryPortalOwner? owner = null)
	{
		var actor = owner?.Actor ?? await BeginCommandAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Renew, input.IdempotencyKey);
		if (owner != null) await RequirePortalLoanAsync(owner, loanId);
		var fingerprint = owner == null ? PayloadHash(new { LoanId = loanId, Request = input }) :
			PayloadHash(new { LoanId = loanId, input.SchoolRenewalDate, input.Revision, owner.StudentId, Source = "Portal" });
		var replay = await ReplayAsync(actor, LibraryLoanOperation.Renew, input.IdempotencyKey, fingerprint, owner);
		if (replay != null) return replay;
		var plan = await BuildRenewalPlanAsync(loanId, new()
		{
			AcademicYearId = input.AcademicYearId, SchoolRenewalDate = input.SchoolRenewalDate, RequestedDueDate = input.RequestedDueDate
		}, owner);
		if (!plan.CanRenew || !plan.NewSchoolDueDate.HasValue)
			throw new BusinessException(LibraryCirculationErrorCodes.RenewalNotAllowed, string.Join(" ", plan.BlockedReasons));
		if (!string.Equals(input.Revision, plan.Revision, StringComparison.Ordinal))
			throw new BusinessException(LibraryCirculationErrorCodes.PlanChanged, "The renewal plan changed. Review the current terms and deadline.");
		if (plan.RequiresCalendarChangeAcknowledgment && string.IsNullOrWhiteSpace(input.CalendarChangeReason))
			throw new BusinessException(LibraryCirculationErrorCodes.CalendarChangeRequiresReview,
				"Review and explain the calendar change before renewing under the current school calendar.");
		var loan = owner == null ? await RequireReadableLoanAsync(loanId) : await RequirePortalLoanAsync(owner, loanId);
		var copy = await copies.GetAsync(loan.BookCopyId);
		var calculation = await calendars.ReadAsync(plan.CurrentAcademicYearId!.Value, copy.SectionId);
		var before = JsonSerializer.Serialize(loan);
		loan.ApplyRenewedDueDate(plan.NewSchoolDueDate.Value);
		await loans.UpdateAsync(loan, true);
		await RecordLoanEventAsync(loan, actor, LibraryLoanEventKind.Renewed,
			string.IsNullOrWhiteSpace(input.Reason) ? "Renewed the loan using its saved borrowing terms." : input.Reason,
			before, new { Version = 1, plan.SchoolRenewalDate, plan.NewSchoolDueDate, plan.CurrentAcademicYearId,
				Calculation = calculation, CalendarChangeReason = input.CalendarChangeReason?.Trim() });
		return await SaveResultAsync(actor, LibraryLoanOperation.Renew, input.IdempotencyKey, fingerprint, loan, owner: owner);
	}

	internal async Task<BookIssue> RequirePortalLoanAsync(LibraryPortalOwner owner, Guid id) =>
		await AsyncExecuter.FirstOrDefaultAsync((await loans.GetQueryableAsync()).Where(row => row.Id == id &&
			row.TenantId == owner.Tenant && row.MemberRole == 0 && Enumerable.Contains(owner.Aliases, row.MemberId)))
			?? throw new AbpAuthorizationException();
}
