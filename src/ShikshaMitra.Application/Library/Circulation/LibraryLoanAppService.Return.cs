using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryLoanAppService
{
	private async Task<LibraryLoanCommandResultDto> ReturnCoreAsync(Guid loanId, LibraryLoanReturnRequest input)
	{
		var actor = await BeginCommandAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return, input.IdempotencyKey);
		var fingerprint = PayloadHash(new { LoanId = loanId, Request = input });
		var replay = await ReplayAsync(actor, LibraryLoanOperation.Return, input.IdempotencyKey, fingerprint);
		if (replay != null) return replay;
		var loan = await RequireReadableLoanAsync(loanId);
		if (loan.Status != BookIssueStatus.Issued) throw new BusinessException(LibraryErrorCodes.ActiveIssueNotFound);
		var copy = await copies.FindAsync(input.CopyId);
		if (copy == null || copy.TenantId != CurrentTenant.Id || loan.BookCopyId != copy.Id)
			throw new BusinessException(LibraryErrorCodes.CopyLoanReconciliationRequired);
		if (!string.Equals(input.Revision, await LoanRevisionAsync(loan, copy), StringComparison.Ordinal))
			throw new BusinessException(LibraryCirculationErrorCodes.PlanChanged, "This loan or copy changed. Review it before receiving the book.");
		if (await AsyncExecuter.AnyAsync((await loans.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			row.Id != loan.Id && row.Status == BookIssueStatus.Issued && (row.BookCopyId == copy.Id ||
			row.AccessionNumber.Trim().ToUpper() == copy.NormalizedAccessionNumber))))
			throw new BusinessException(LibraryErrorCodes.CopyLoanReconciliationRequired);
		var finalStatus = (BookCopyStatus)input.CopyStatus;
		if (finalStatus is not (BookCopyStatus.Available or BookCopyStatus.Damaged or BookCopyStatus.UnderRepair))
			throw new BusinessException(LibraryErrorCodes.InvalidCopyStatusTransition);
		var preparedAssessments = new List<LibraryCharge>();
		foreach (var assessment in input.Assessments)
			preparedAssessments.Add(await PrepareReturnAssessmentAsync(loan, assessment, actor));
		var before = JsonSerializer.Serialize(loan);
		loan.ReceivePhysicalCopy(input.SchoolReturnDate, !string.IsNullOrWhiteSpace(input.UnknownOriginalDatesReason));
		if (input.Remarks != null) loan.Remarks = input.Remarks.Trim();
		var assessmentIds = new List<Guid>();
		foreach (var assessment in preparedAssessments)
		{
			await charges.InsertAsync(assessment, true);
			assessmentIds.Add(assessment.Id);
		}
		await loans.UpdateAsync(loan, true);
		var copyBefore = LibraryCopySnapshot.Capture(copy);
		copy.Status = finalStatus;
		if (input.Remarks != null) copy.Remarks = input.Remarks.Trim();
		await copyEvents.RecordAsync(copyBefore, copy, LibraryCopyEventType.Returned, actor, "Received the physical copy.", loan.Id);
		await copies.UpdateAsync(copy, true);
		await RecordLoanEventAsync(loan, actor, LibraryLoanEventKind.Returned,
			input.UnknownOriginalDatesReason?.Trim() is { Length: > 0 } reason ? reason : "Received the physical copy.", before);
		if (copy.Status == BookCopyStatus.Available) await reservations.RebalanceAsync(copy.BookId, input.SchoolReturnDate);
		await notifications.SendReturnNotificationsAsync(loan, await books.GetAsync(copy.BookId), copy);
		return await SaveResultAsync(actor, LibraryLoanOperation.Return, input.IdempotencyKey, fingerprint, loan, assessmentIds);
	}

	private async Task<LibraryCharge> PrepareReturnAssessmentAsync(BookIssue loan, LibraryReturnAssessmentRequest input, Guid actor)
	{
		var yearId = input.AcademicYearId ?? loan.AcademicYearId;
		var explicitContext = input.AcademicYearId.HasValue && input.AcademicYearId != loan.AcademicYearId;
		if (!yearId.HasValue || explicitContext && string.IsNullOrWhiteSpace(input.AcademicYearReason))
			throw new BusinessException(LibraryErrorCodes.ContextInvalid, "Select and explain the academic context for this new assessment.");
		await access.GetContextAsync(yearId);
		var category = await chargeCategories.FindAsync(input.ChargeCategoryId);
		if (category == null || category.TenantId != CurrentTenant.Id || category.AcademicYearId != yearId.Value)
			throw new BusinessException(LibraryErrorCodes.ChargeCategoryNotFound);
		if (category.IsFixed && input.Amount != category.Amount)
			throw new BusinessException(LibraryErrorCodes.InvalidCharge,
				"This category has a fixed assessment amount. Review its current amount before submitting the return.");
		var facts = (await eligibility.ReadAsync([loan.MemberId])).GetValueOrDefault(loan.MemberId)
			?? throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		if (explicitContext)
		{
			var readable = facts.Role == 0
				? await AsyncExecuter.AnyAsync((await access.GetStudentsAsync()).Where(row => row.Id == facts.PersonId && row.AcademicYearId == yearId))
				: await AsyncExecuter.AnyAsync((await access.GetEmployeesAsync()).Where(row => row.Id == facts.PersonId));
			if (!readable) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		}
		var yearEvidence = JsonSerializer.Serialize(new { LoanId = loan.Id, AcademicYearId = yearId, input.AcademicYearReason,
			OriginalLoanYear = loan.AcademicYearId, ActorId = actor });
		var dateEvidence = JsonSerializer.Serialize(new { LoanId = loan.Id, input.SchoolChargeDate, input.SchoolDueDate, ActorId = actor });
		return LibraryCharge.CreateConfirmed(GuidGenerator.Create(), facts.PersonId, facts.Role, category.Id, loan.Id,
			input.Amount, input.SchoolChargeDate, input.SchoolDueDate, yearId.Value,
			explicitContext ? LibraryAcademicYearProvenance.ExplicitAssessmentContext : LibraryAcademicYearProvenance.ConfirmedLoanContext,
			yearEvidence, dateEvidence, input.Reason, CurrentTenant.Id!.Value);
	}
}
