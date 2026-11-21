using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Reports;

public partial class LibraryReportGenerator
{
	private static IEnumerable<BookIssue> LoanPopulation(LibraryReportContext context, LibraryReportFilterDto filters) =>
		context.Issues.Where(issue => MatchesMember(context, filters, issue.MemberId, context.PersonId(issue)))
			.Where(issue => !filters.MemberRole.HasValue || issue.MemberRole == filters.MemberRole.Value)
			.Where(issue => filters.IssueStatuses is not { Count: > 0 } || filters.IssueStatuses.Contains((int)issue.Status));

	private static bool MatchesMember(LibraryReportContext context, LibraryReportFilterDto filters, Guid recordedId, Guid? canonicalId = null) =>
		filters.MemberIds is not { Count: > 0 } || filters.MemberIds.Contains(recordedId) ||
		canonicalId.HasValue && filters.MemberIds.Contains(canonicalId.Value) ||
		filters.MemberIds.Any(id => context.MemberAliases.GetValueOrDefault(id, id) == (canonicalId ?? recordedId));

	private static DateOnly? ConfirmedRequestDate(BookReservation request) =>
		LibraryLoanDates.IsConfirmed(request.RequestedDateProvenance) ? request.SchoolRequestedDate : null;

	private static string LoanDateEvidence(BookIssue loan)
	{
		var dates = new List<bool> { LibraryLoanDates.ConfirmedIssueDate(loan).HasValue, LibraryLoanDates.ConfirmedDueDate(loan).HasValue };
		if (loan.Status == BookIssueStatus.Returned) dates.Add(LibraryLoanDates.ConfirmedReturnDate(loan).HasValue);
		return dates.All(confirmed => confirmed) ? "Confirmed school dates" : dates.Any(confirmed => confirmed) ? "Partly confirmed" : "Unconfirmed";
	}

	private static string RequestDateEvidence(BookReservation request) => ConfirmedRequestDate(request).HasValue &&
		(!request.SchoolRequestExpiryDate.HasValue && !request.ReserveDueDate.HasValue || LibraryLoanDates.IsConfirmed(request.RequestExpiryDateProvenance))
		? "Confirmed school dates" : "Unconfirmed";

	private static bool DateInRange(DateOnly? date, LibraryReportFilterDto filters)
	{
		if (!date.HasValue) return !filters.FromDate.HasValue && !filters.ToDate.HasValue;
		return (!filters.FromDate.HasValue || date >= DateOnly.FromDateTime(filters.FromDate.Value)) &&
			(!filters.ToDate.HasValue || date <= DateOnly.FromDateTime(filters.ToDate.Value));
	}

	private static void ApplyDateEvidence(LibraryReportPreviewDto report, LibraryReportContext context, LibraryReportFilterDto filters)
	{
		var day = context.ReadDay;
		var population = LoanPopulation(context, filters).ToList();
		var unknownDue = LibraryReadDateEvidence.UnknownDueDates(population);
		report.SchoolAsOfDate = day.Day;
		report.SchoolDateSource = day.Source;
		report.SchoolDateUnavailableReason = day.UnavailableReason;
		report.UnknownIssueDateCount = LibraryReadDateEvidence.UnknownIssueDates(population);
		report.UnknownReturnDateCount = LibraryReadDateEvidence.UnknownReturnDates(population);
		report.UnknownDueDateCount = unknownDue;
		report.OverdueAsOfDate = day.Day;
		report.OverdueDateSource = day.Source;
		report.OverdueClassificationAvailable = day.Day.HasValue && unknownDue == 0;
		report.OverdueUnavailableReason = LibraryReadDateEvidence.OverdueReason(day, unknownDue);
		if (report.ReportType is LibraryReportTypes.OverdueBooks or LibraryReportTypes.IssueReturn or LibraryReportTypes.MemberList)
		{
			if (report.OverdueUnavailableReason != null) report.QueryWarnings.Add(report.OverdueUnavailableReason);
			if (report.UnknownIssueDateCount > 0 || report.UnknownReturnDateCount > 0)
				report.QueryWarnings.Add("Some movement dates are unconfirmed. School-date ranges exclude those records.");
		}
	}
}
