using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using Volo.Abp;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryIssuePlanAppService
{
	private static DateOnly? ResolveDueDate(LibraryIssuePlanRequest input, LibraryBorrowingPolicyDecision decision,
		LibraryCalendarEvidence calendar, DateTimeOffset now)
	{
		if (input.RequestedDueDate.HasValue && input.RequestedDueDate < input.IssueDate)
			throw new BusinessException(LibraryErrorCodes.InvalidIssueDateRange);
		if (decision.Policy == null || decision.Policy.Terms.DueDateMode == LibraryDueDateMode.Manual) return input.RequestedDueDate;
		var schoolToday = calendar.ConfirmedSchoolDateAt(now);
		if (!schoolToday.HasValue)
			throw new BusinessException(LibraryErrorCodes.CalendarNotConfigured);
		if (input.IssueDate > schoolToday.Value)
			throw new BusinessException(LibraryErrorCodes.InvalidIssueDateRange,
				"An automated checkout cannot use an issue date after the current confirmed school day.");
		var terms = decision.Policy.Terms;
		var due = LibraryCalendarCalculator.Calculate(input.IssueDate, terms.LoanDays, terms.DayCountingMode,
			terms.MoveDueDateToNextOpenDay, calendar.OpenWeekdays, calendar.ClosedDates);
		if (input.RequestedDueDate.HasValue && input.RequestedDueDate.Value != due)
			throw new BusinessException(LibraryErrorCodes.PolicyInvalid, "This policy calculates the due date. Review a permitted exception before changing it.");
		return due;
	}

	private static string Fingerprint(object evidence) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evidence))));
}
