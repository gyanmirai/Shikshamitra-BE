using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Portal;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryLoanAppService
{
	private static readonly JsonSerializerOptions LoanJsonOptions = new() { PropertyNameCaseInsensitive = true };

	private async Task<LibraryLoanDto> ReadLoanAsync(Guid id)
	{
		var loan = await RequireReadableLoanAsync(id);
		return await ReadLoanAsync(loan);
	}

	private async Task<LibraryLoanDto> ReadLoanAsync(BookIssue loan, LibraryPortalOwner? portalOwner = null)
	{
		var facts = (await eligibility.ReadAsync([loan.MemberId])).GetValueOrDefault(loan.MemberId);
		var currentBorrowerInScope = portalOwner != null
			? facts?.PersonId == portalOwner.StudentId && facts.Role == 0
			: facts != null && (facts.Role == 0
			? await AsyncExecuter.AnyAsync((await access.GetStudentsAsync()).Where(row => row.Id == facts.PersonId))
			: await AsyncExecuter.AnyAsync((await access.GetEmployeesAsync()).Where(row => row.Id == facts.PersonId)));
		var copy = await copies.FindAsync(loan.BookCopyId);
		var confirmedCopy = copy != null && copy.TenantId == CurrentTenant.Id;
		var open = loan.Status == BookIssueStatus.Issued;
		var policy = loan.PolicySnapshotJson == null ? null : JsonSerializer.Deserialize<LibraryBorrowingPolicySnapshotDto>(loan.PolicySnapshotJson, LoanJsonOptions);
		return new()
		{
			Id = loan.Id, CopyId = loan.BookCopyId, BookId = confirmedCopy ? copy!.BookId : null,
			MemberId = loan.MemberId, CanonicalMemberId = facts?.PersonId, Role = loan.MemberRole, AcademicYearId = loan.AcademicYearId,
			Status = (int)loan.Status, SchoolIssueDate = LibraryLoanDates.DisplayIssueDate(loan),
			SchoolDueDate = LibraryLoanDates.DisplayDueDate(loan), SchoolReturnDate = LibraryLoanDates.DisplayReturnDate(loan),
			OriginalSchoolDueDate = loan.OriginalSchoolDueDate, IssueDateProvenance = (int)loan.IssueDateProvenance,
			DueDateProvenance = (int)loan.DueDateProvenance, ReturnDateProvenance = (int)loan.ReturnDateProvenance,
			RawIssueDate = loan.IssueDate, RawDueDate = loan.DueDate, RawReturnDate = loan.ReturnDate,
			BorrowerScopeCaptureId = loan.BorrowerScopeCaptureId, BorrowerScopeCaptureVersion = loan.BorrowerScopeCaptureVersion,
			BorrowerScopeProvenance = (int)loan.BorrowerScopeProvenance,
			IssuedByUserId = loan.IssuedByUserId, RenewalCount = loan.RenewalCount, Policy = policy,
			Revision = await LoanRevisionAsync(loan, copy),
			CanReturn = portalOwner == null && open && confirmedCopy && await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return),
			CanRenew = open && confirmedCopy && currentBorrowerInScope && facts?.Eligibility == LibraryReservationEligibility.Eligible &&
				policy?.Terms.MaxRenewals > loan.RenewalCount &&
				LibraryLoanDates.ConfirmedDueDate(loan).HasValue && (portalOwner != null ||
					await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Renew)),
			BlockedReasons = !confirmedCopy ? ["Review the loan's physical copy reference before receiving it."] :
				!LibraryLoanDates.ConfirmedIssueDate(loan).HasValue || !LibraryLoanDates.ConfirmedDueDate(loan).HasValue
					? ["The original school dates need review. A physical return can retain that uncertainty with an explicit reason."] : []
		};
	}

	private async Task<BookIssue> RequireReadableLoanAsync(Guid id)
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)) throw new AbpAuthorizationException();
		var context = await access.GetContextAsync();
		var loan = await loans.FindAsync(id);
		if (loan == null || loan.TenantId != CurrentTenant.Id) throw new BusinessException(LibraryCirculationErrorCodes.LoanNotFound);
		if (!await recordedLoanScope.CanReadAsync(id, context.UserScope)) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return loan;
	}

	private async Task<string> LoanRevisionAsync(BookIssue loan, BookCopy? copy)
	{
		var evidence = JsonSerializer.Serialize(new
		{
			loan.Id, loan.TenantId, loan.BookCopyId, loan.MemberId, loan.Status, loan.ConcurrencyStamp,
			loan.SchoolIssueDate, loan.SchoolDueDate, loan.OriginalSchoolDueDate, loan.SchoolReturnDate,
			loan.IssueDateProvenance, loan.DueDateProvenance, loan.ReturnDateProvenance, loan.RenewalCount,
			loan.AcademicYearId, loan.PolicySnapshotJson, loan.BorrowerSnapshotJson, loan.OriginalCalendarSnapshotJson,
			loan.BorrowerScopeCaptureId, loan.BorrowerScopeCaptureVersion, loan.BorrowerScopeProvenance,
			CopyRevision = copy == null ? null : await copyEvents.RevisionAsync(copy, loan.Status == BookIssueStatus.Issued ? loan.Id : null)
		});
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
	}
}
