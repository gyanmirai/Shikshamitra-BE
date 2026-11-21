using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Shared;

internal sealed record LibraryReadSchoolDay(DateOnly? Day, string Source, string? UnavailableReason);

internal static class LibraryReadDateEvidence
{
	public static async Task<LibraryReadSchoolDay> ReadAsync(LibraryCalendarService calendar, IClock clock, DateOnly? explicitDate = null)
	{
		if (explicitDate.HasValue) return new(explicitDate, "ExplicitSchoolDate", null);
		var settings = await calendar.GetSettingsAsync();
		if (settings.Source != "Library" || settings.NeedsConfiguration)
			return new(null, "Unavailable", "Confirm the Library timezone or choose an explicit school as-of date before classifying date-dependent totals.");
		return new(LibraryCalendarCalculator.SchoolDate(new DateTimeOffset(clock.Now), LibraryCalendarService.ResolveZone(settings.TimeZoneId)),
			"ConfirmedLibraryClock", null);
	}

	public static int UnknownIssueDates(IEnumerable<BookIssue> loans) => loans.Count(loan => !LibraryLoanDates.ConfirmedIssueDate(loan).HasValue);
	public static int UnknownReturnDates(IEnumerable<BookIssue> loans) => loans.Count(loan => loan.Status == BookIssueStatus.Returned &&
		!LibraryLoanDates.ConfirmedReturnDate(loan).HasValue);
	public static int UnknownDueDates(IEnumerable<BookIssue> loans) => loans.Count(loan => loan.Status == BookIssueStatus.Issued &&
		!LibraryLoanDates.ConfirmedDueDate(loan).HasValue);
	public static string? OverdueReason(LibraryReadSchoolDay day, int unknownDue)
	{
		var reasons = new List<string>();
		if (!day.Day.HasValue) reasons.Add(day.UnavailableReason ?? "The school as-of date is unavailable.");
		if (unknownDue > 0) reasons.Add("Some open loans have unconfirmed due dates. Review them before treating this total as complete.");
		return reasons.Count == 0 ? null : string.Join(" ", reasons);
	}
}
