using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Circulation;
using ShikshaMitra.Library.Reservations;

namespace ShikshaMitra.Library.Reports;

public partial class LibraryReportGenerator
{
	private static List<Dictionary<string, string?>> BuildIssueReturnRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		FilterIssues(context, filters, LibraryLoanDates.ConfirmedIssueDate)
			.Select(issue => BuildIssueRow(context, issue))
			.ToList();

	private static List<Dictionary<string, string?>> BuildOverdueRows(LibraryReportContext context, LibraryReportFilterDto filters)
	{
		var today = context.ReadDay.Day;
		if (!today.HasValue) return [];
		return FilterIssues(context, filters, LibraryLoanDates.ConfirmedDueDate)
			.Where(issue => issue.Status == BookIssueStatus.Issued && LibraryLoanDates.ConfirmedDueDate(issue) < today)
			.Select(issue =>
			{
				var row = BuildIssueRow(context, issue);
				row["overdueDays"] = (today.Value.DayNumber - LibraryLoanDates.ConfirmedDueDate(issue)!.Value.DayNumber).ToString(CultureInfo.InvariantCulture);
				return row;
			})
			.ToList();
	}

	private async Task<List<Dictionary<string, string?>>> BuildMemberRowsAsync(LibraryReportContext context, LibraryReportFilterDto filters)
	{
		var members = context.Members.Values
			.DistinctBy(member => member.Id)
			.Where(member => member.IsCurrentMember)
			.Where(member => MatchesMember(context, filters, member.Id))
			.Where(member => !filters.MemberRole.HasValue || member.RoleValue == filters.MemberRole.Value)
			.ToArray();
		var finance = await ReadMemberFinanceAsync(members, filters.AcademicYearId);
		var issues = context.Issues.Where(issue => issue.Status == BookIssueStatus.Issued)
			.ToLookup(issue => (issue.MemberRole, context.PersonId(issue)));
		var reservations = context.Reservations.Where(row => row.State is LibraryReservationState.Waiting or LibraryReservationState.Ready)
			.ToLookup(row => (row.MemberRole, context.PersonId(row)));
		var declarations = context.Charges.Where(row => row.PaymentStatus == ChargePaymentStatus.Pending && row.MemberRole is 0 or 1 or 2)
			.ToLookup(row => (row.MemberRole == 0 ? 0 : 1,
				row.MemberRole == 0 ? context.MemberAliases.GetValueOrDefault(row.MemberId, row.MemberId) : row.MemberId));
		return members.Select(member =>
			{
				var memberKey = (member.RoleValue, member.Id);
				var memberIssues = issues[memberKey];
				var activeIssues = memberIssues.Count();
				var overdueIssues = memberIssues.Count(x => context.ReadDay.Day.HasValue && LibraryLoanDates.ConfirmedDueDate(x) < context.ReadDay.Day);
				var pendingCharges = declarations[memberKey].Sum(x => x.Amount);
				var position = MemberFinanceColumns.From(finance.GetValueOrDefault(memberKey) ?? []);

				return Row(
					("memberName", member.Name),
					("memberIdentifier", member.Identifier),
					("memberRole", member.Role),
					("email", member.Email),
					("phone", member.Phone),
					("classDivision", member.ClassDivision),
					("joinDate", Date(member.JoinDate)),
					("activeIssues", activeIssues.ToString(CultureInfo.InvariantCulture)),
					("overdueIssues", overdueIssues.ToString(CultureInfo.InvariantCulture)),
					("reservationCount", reservations[memberKey].Count().ToString(CultureInfo.InvariantCulture)),
					("pendingCharges", Money(pendingCharges)),
					("financeEvidence", position.Evidence),
					("currency", position.Currency),
					("collectibleAmountText", FinanceText(position.Collectible)));
			})
			.OrderBy(x => x["memberName"])
			.ToList();
	}

	private static List<Dictionary<string, string?>> BuildReservationRows(LibraryReportContext context, LibraryReportFilterDto filters) =>
		context.Reservations
			.Where(reservation => MatchesMember(context, filters, reservation.MemberId, context.PersonId(reservation)))
			.Where(reservation => !filters.MemberRole.HasValue || reservation.MemberRole == filters.MemberRole.Value)
			.Where(reservation => DateInRange(ConfirmedRequestDate(reservation), filters))
			.Select(reservation =>
			{
				var book = context.BookById.GetValueOrDefault(reservation.BookId);
				var member = context.Members.GetValueOrDefault(context.PersonId(reservation));
				if (reservation.MemberRole.HasValue && member?.RoleValue != reservation.MemberRole) member = null;
				return Row(
					("requestedDate", Date(reservation.RequestedDate)),
					("reserveDueDate", Date(reservation.ReserveDueDate)),
					("rawRequestedDate", RawDate(reservation.RequestedDate)),
					("rawReserveDueDate", RawDate(reservation.ReserveDueDate)),
					("schoolRequestedDate", Date(ConfirmedRequestDate(reservation))),
					("schoolRequestExpiryDate", Date(LibraryLoanDates.IsConfirmed(reservation.RequestExpiryDateProvenance) ? reservation.SchoolRequestExpiryDate : null)),
					("requestedDateProvenance", ((int)reservation.RequestedDateProvenance).ToString(CultureInfo.InvariantCulture)),
					("requestExpiryDateProvenance", ((int)reservation.RequestExpiryDateProvenance).ToString(CultureInfo.InvariantCulture)),
					("dateEvidence", RequestDateEvidence(reservation)),
					("academicYearId", reservation.AcademicYearId?.ToString()),
					("title", book?.Title),
					("memberRecordId", context.PersonId(reservation).ToString()),
					("memberName", member?.Name),
					("memberIdentifier", member?.Identifier),
					("memberRole", reservation.MemberRole == 0 ? "Student" : reservation.MemberRole == 1 ? "Staff" : member?.Role),
					("memberDisplaySource", member == null ? "Unavailable" : "CurrentAuthorizedRecord"),
					("state", ((int)reservation.State).ToString(CultureInfo.InvariantCulture)),
					("status", reservation.State == LibraryReservationState.NeedsReview ? "Needs review" : reservation.State.ToString()));
			})
			.ToList();

	private static Dictionary<string, string?> BuildIssueRow(LibraryReportContext context, BookIssue issue)
	{
		var book = context.ResolveBook(issue);
		var member = context.Members.GetValueOrDefault(context.PersonId(issue));
		if (member?.RoleValue != issue.MemberRole) member = null;
		var snapshot = LibraryLoanBorrowerSnapshot.Read(issue, context.PersonId(issue));
		return Row(
			("accessionNumber", issue.AccessionNumber),
			("title", book?.Title),
			("memberRecordId", context.PersonId(issue).ToString()),
			("memberName", snapshot != null ? snapshot.MemberName : member?.Name),
			("memberIdentifier", snapshot != null ? snapshot.MemberIdentifier : member?.Identifier),
			("classDivision", snapshot != null ? snapshot.ClassDivision : member?.ClassDivision),
			("memberDisplaySource", snapshot != null ? "LoanSnapshot" : member != null ? "CurrentAuthorizedRecord" : "Unavailable"),
			("memberRole", issue.MemberRole == 0 ? "Student" : issue.MemberRole == 1 ? "Staff" : "Unknown"),
			("academicYearId", issue.AcademicYearId?.ToString()),
			("copyId", issue.BookCopyId == Guid.Empty ? null : issue.BookCopyId.ToString()),
			("copyLinkSource", book == null ? "Unavailable" : issue.BookCopyId == Guid.Empty ? "LegacyAccessionCandidate" : "CopyId"),
			("issueDate", Date(issue.IssueDate)),
			("dueDate", Date(issue.DueDate)),
			("returnDate", Date(issue.ReturnDate)),
			("rawIssueDate", RawDate(issue.IssueDate)),
			("rawDueDate", RawDate(issue.DueDate)),
			("rawReturnDate", RawDate(issue.ReturnDate)),
			("schoolIssueDate", Date(LibraryLoanDates.ConfirmedIssueDate(issue))),
			("schoolDueDate", Date(LibraryLoanDates.ConfirmedDueDate(issue))),
			("schoolReturnDate", Date(LibraryLoanDates.ConfirmedReturnDate(issue))),
			("issueDateProvenance", ((int)issue.IssueDateProvenance).ToString(CultureInfo.InvariantCulture)),
			("dueDateProvenance", ((int)issue.DueDateProvenance).ToString(CultureInfo.InvariantCulture)),
			("returnDateProvenance", ((int)issue.ReturnDateProvenance).ToString(CultureInfo.InvariantCulture)),
			("dateEvidence", LoanDateEvidence(issue)),
			("status", issue.Status.ToString()),
			("amount", issue.Amount.HasValue ? Money(issue.Amount.Value) : null));
	}

	private static IEnumerable<BookIssue> FilterIssues(
		LibraryReportContext context,
		LibraryReportFilterDto filters,
		Func<BookIssue, DateOnly?> dateSelector) =>
		LoanPopulation(context, filters)
			.Where(issue => DateInRange(dateSelector(issue), filters));
}
