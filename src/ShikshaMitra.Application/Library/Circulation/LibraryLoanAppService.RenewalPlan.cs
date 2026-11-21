using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Portal;
using ShikshaMitra.Library.Reservations;
using Volo.Abp;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryLoanAppService
{
	internal async Task<LibraryLoanRenewalPlanDto> BuildRenewalPlanAsync(Guid loanId, LibraryLoanRenewalPlanRequest input,
		LibraryPortalOwner? owner = null)
	{
		if (owner == null && !await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Renew))
			throw new AbpAuthorizationException();
		var loan = owner == null ? await RequireReadableLoanAsync(loanId) : await RequirePortalLoanAsync(owner, loanId);
		var details = await ReadLoanAsync(loan, owner);
		var result = new LibraryLoanRenewalPlanDto { Loan = details, SchoolRenewalDate = input.SchoolRenewalDate };
		var policy = details.Policy;
		var previous = LibraryLoanDates.ConfirmedDueDate(loan);
		if (input.SchoolRenewalDate == default) result.BlockedReasons.Add("Enter the school's renewal date.");
		if (!details.CanRenew || policy == null || !previous.HasValue)
			result.BlockedReasons.Add("The loan's eligibility, saved renewal terms, or confirmed school dates do not permit renewal.");
		var copy = await copies.FindAsync(loan.BookCopyId);
		if (copy == null || copy.TenantId != CurrentTenant.Id || copy.Status != BookCopyStatus.Issued)
			result.BlockedReasons.Add("Review the physical copy's loan and condition before renewing.");
		if (result.BlockedReasons.Count > 0)
		{
			result.Revision = PayloadHash(new { details.Revision, Input = input, result.BlockedReasons });
			return result;
		}
		var borrower = owner == null ? await borrowers.ResolveAsync(details.CanonicalMemberId!.Value.ToString()) : null;
		var borrowerId = owner?.StudentId ?? borrower!.Id;
		var yearId = owner != null ? owner.Facts.AcademicYearId : borrower!.Role == 0 ? borrower.AcademicYearId : input.AcademicYearId;
		if (!yearId.HasValue || input.AcademicYearId.HasValue && input.AcademicYearId != yearId)
			throw new BusinessException(LibraryErrorCodes.AcademicYearRequired, "Select the staff renewal year, or use the student's actual current year.");
		if (owner == null) await access.GetContextAsync(yearId);
		await eligibility.RequireAsync(borrowerId, yearId.Value);
		result.CurrentAcademicYearId = yearId;
		var calendar = await calendars.ReadAsync(yearId.Value, copy!.SectionId);
		result.CurrentSchoolTimeZoneId = calendar.Settings.Source == "Library" && !calendar.Settings.NeedsConfiguration ? calendar.Settings.TimeZoneId : null;
		var waiting = await AsyncExecuter.ToListAsync((await reservationRequests.GetQueryableAsync()).Where(row =>
			row.TenantId == CurrentTenant.Id && row.BookId == copy.BookId &&
			(!row.PickupSectionId.HasValue || row.PickupSectionId == copy.SectionId) &&
			(row.State == LibraryReservationState.Waiting || row.State == LibraryReservationState.Ready || row.State == LibraryReservationState.NeedsReview) &&
			(!row.CanonicalMemberId.HasValue || row.CanonicalMemberId != borrowerId))
			.OrderBy(row => row.Id).Select(row => new { row.Id, row.State, row.Revision }));
		if (waiting.Count > 0) result.BlockedReasons.Add("Reservation priority must be resolved before extending this loan.");
		var activeLoans = owner == null ? borrower!.Statistics.CurrentIssue : await AsyncExecuter.CountAsync((await loans.GetQueryableAsync())
			.Where(row => row.TenantId == owner.Tenant && row.MemberRole == 0 && row.Status == BookIssueStatus.Issued &&
				Enumerable.Contains(owner.Aliases, row.MemberId)));
		if (activeLoans > policy!.Terms.MaxBooksAtTime) result.BlockedReasons.Add("Resolve this borrower's active loan limit before renewing.");
		var manual = policy.Terms.DueDateMode == (int)LibraryDueDateMode.Manual;
		if (manual)
		{
			result.NewSchoolDueDate = input.RequestedDueDate;
			if (owner != null) result.BlockedReasons.Add("Ask your school library to set the new due date.");
			else if (!input.RequestedDueDate.HasValue) result.BlockedReasons.Add("Enter the school's new due date.");
		}
		else if (result.CurrentSchoolTimeZoneId == null)
			result.BlockedReasons.Add("Confirm the current Library calendar before calculating an automated renewal.");
		else if (input.SchoolRenewalDate > calendar.ConfirmedSchoolDateAt(new DateTimeOffset(Clock.Now.ToUniversalTime())))
			result.BlockedReasons.Add("An automated renewal cannot use a renewal date after the current confirmed school day.");
		else if (!policy.Terms.RenewalDays.HasValue || !policy.Terms.DayCountingMode.HasValue)
			result.BlockedReasons.Add("Review the saved renewal calculation terms.");
		else
		{
			var start = previous!.Value > input.SchoolRenewalDate ? previous.Value : input.SchoolRenewalDate;
			result.NewSchoolDueDate = LibraryCalendarCalculator.Calculate(start, policy.Terms.RenewalDays.Value,
				(LibraryDayCountingMode)policy.Terms.DayCountingMode.Value, policy.Terms.MoveDueDateToNextOpenDay ?? true,
				calendar.OpenWeekdays, calendar.ClosedDates);
			if (input.RequestedDueDate.HasValue && input.RequestedDueDate != result.NewSchoolDueDate)
				result.BlockedReasons.Add("The saved policy calculates this renewal's due date.");
			result.RequiresCalendarChangeAcknowledgment = !string.Equals(policy.TimeZoneId, result.CurrentSchoolTimeZoneId, StringComparison.Ordinal);
			if (owner != null && input.SchoolRenewalDate != calendar.ConfirmedSchoolDateAt(new DateTimeOffset(Clock.Now.ToUniversalTime())))
				result.BlockedReasons.Add("The school day changed. Review the renewal again.");
			if (owner != null && result.RequiresCalendarChangeAcknowledgment)
				result.BlockedReasons.Add("Ask your school library to review the calendar change.");
		}
		if (result.NewSchoolDueDate.HasValue && (result.NewSchoolDueDate <= previous || result.NewSchoolDueDate < input.SchoolRenewalDate))
			result.BlockedReasons.Add("The new deadline must extend the existing due date and cover the renewal date.");
		result.CanRenew = result.BlockedReasons.Count == 0;
		result.Revision = PayloadHash(new { details.Revision, Input = input, result.NewSchoolDueDate, Calendar = calendar.Revision,
			Borrower = owner == null ? (object?)borrower : new { owner.StudentId, activeLoans }, Waiting = waiting,
			YearId = yearId, result.RequiresCalendarChangeAcknowledgment, result.BlockedReasons });
		return result;
	}
}
