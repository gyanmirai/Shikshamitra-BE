using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryLoanAppService
{
	private async Task<LibraryLoanCommandResultDto> CheckoutCoreAsync(LibraryCheckoutRequest input)
	{
		var actor = await BeginCommandAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue, input.IdempotencyKey);
		var fingerprint = PayloadHash(input);
		var replay = await ReplayAsync(actor, LibraryLoanOperation.Checkout, input.IdempotencyKey, fingerprint);
		if (replay != null) return replay;
		var plan = await plans.PreviewAsync(new()
		{
			CopyId = input.CopyId, MemberId = input.MemberId, AcademicYearId = input.AcademicYearId,
			IssueDate = input.IssueDate, RequestedDueDate = input.RequestedDueDate
		});
		if (!string.Equals(input.Revision, plan.Revision, StringComparison.Ordinal))
			throw new BusinessException(LibraryCirculationErrorCodes.PlanChanged, "The checkout plan changed. Review the current copy, borrower, and due date before submitting.");
		await eligibility.RequireAsync(plan.MemberId, plan.AcademicYearId);
		if (!plan.CanIssue || !plan.DueDate.HasValue)
			throw new BusinessException(plan.HasOpenLoan ? LibraryErrorCodes.DuplicateActiveIssue :
				plan.ActiveLoanCount >= plan.Policy.Terms.MaxBooksAtTime ? LibraryErrorCodes.BorrowingLimitExceeded : LibraryErrorCodes.CopyNotAvailable,
				string.Join(" ", plan.BlockedReasons));
		var copy = await copies.GetAsync(plan.CopyId);
		if (copy.TenantId != CurrentTenant.Id) throw new BusinessException(LibraryErrorCodes.CopyNotFound);
		var borrower = await borrowers.ResolveAsync(plan.MemberId.ToString());
		var calendar = await calendars.ReadAsync(plan.AcademicYearId, copy.SectionId);
		var student = borrower.Role == 0 ? await AsyncExecuter.FirstOrDefaultAsync((await access.GetStudentsAsync())
			.Where(row => row.Id == plan.MemberId).Select(row => new
			{
				row.AcademicBoardId, row.AcademicMediumId, row.AcademicMediumStageId, row.StandardId, row.DivisionId
			})) : null;
		var borrowerSnapshot = JsonSerializer.Serialize(new
		{
			Version = 1, borrower.Id, borrower.MemberName, borrower.MemberIdentifier, borrower.Role, borrower.ClassDivision,
			AcademicYearId = plan.AcademicYearId, AcademicContext = student
		});
		var context = new LibraryLoanIssueContext(plan.AcademicYearId, student?.AcademicBoardId, student?.AcademicMediumId,
			student?.AcademicMediumStageId, student?.StandardId, student?.DivisionId, JsonSerializer.Serialize(plan.Policy),
			borrowerSnapshot, JsonSerializer.Serialize(calendar), actor, new DateTimeOffset(Clock.Now.ToUniversalTime()));
		await reservations.PrepareIssueAsync(copy, borrower.Id, plan.IssueDate);
		var loan = BookIssue.CreateCivil(GuidGenerator.Create(), CurrentTenant.Id, copy.Id, copy.AccessionNumber,
			borrower.Id, borrower.Role, plan.IssueDate, plan.DueDate.Value, context);
		if (borrower.Role == 1) loan.BindStaffScopeCapture(await staffCaptures.CaptureAsync(LibraryBorrowerScopeSourceKind.Loan,
			loan.Id, borrower.Id, plan.AcademicYearId, LibraryBorrowerScopeProvenance.CapturedAtOperation, LibraryOperationActor.User(actor)));
		loan.Remarks = input.Remarks?.Trim();
		await loans.InsertAsync(loan, true);
		var before = LibraryCopySnapshot.Capture(copy);
		copy.Status = BookCopyStatus.Issued;
		await copyEvents.RecordAsync(before, copy, LibraryCopyEventType.Issued, actor, "Checked out the physical copy.", loan.Id);
		await copies.UpdateAsync(copy, true);
		await reservations.CompleteIssueAsync(copy, borrower.Id, loan.Id, LibraryOperationActor.User(actor));
		await RecordLoanEventAsync(loan, actor, LibraryLoanEventKind.Issued, "Checked out the physical copy with saved borrowing terms.", "{}");
		await notifications.SendIssueConfirmationAsync(loan, await books.GetAsync(copy.BookId), copy);
		return await SaveResultAsync(actor, LibraryLoanOperation.Checkout, input.IdempotencyKey, fingerprint, loan);
	}
}
