using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryCirculationReadService
{
	private async Task<ReadAsOfEvidence> ReadAsOfAsync(DateOnly? supplied)
	{
		if (supplied.HasValue) return new(supplied, "ExplicitSchoolDate", null);
		var settings = await calendar.GetSettingsAsync();
		if (settings.Source != "Library" || settings.NeedsConfiguration)
			return new(null, "Unavailable", "A confirmed Library clock or an explicit school as-of date is required to classify overdue loans.");
		return new(LibraryCalendarCalculator.SchoolDate(new DateTimeOffset(clock.Now), LibraryCalendarService.ResolveZone(settings.TimeZoneId)),
			"ConfirmedLibraryClock", null);
	}

	private static void ApplyDates(IssueReturnItemDto dto, BookIssue loan, ReadAsOfEvidence asOf)
	{
		dto.AcademicYearId = loan.AcademicYearId;
		dto.SchoolIssueDate = loan.SchoolIssueDate;
		dto.SchoolDueDate = loan.SchoolDueDate;
		dto.SchoolReturnDate = loan.SchoolReturnDate;
		dto.IssueDateProvenance = (int)loan.IssueDateProvenance;
		dto.DueDateProvenance = (int)loan.DueDateProvenance;
		dto.ReturnDateProvenance = (int)loan.ReturnDateProvenance;
		dto.IssueDate = dto.RawIssueDate = loan.IssueDate;
		dto.DueDate = dto.RawDueDate = loan.DueDate;
		dto.ReturnDate = dto.RawReturnDate = loan.ReturnDate;
		dto.Status = dto.StoredStatus = (int)loan.Status;
		dto.OverdueAsOfDate = asOf.Date;
		dto.OverdueDateSource = asOf.Source;
		if (loan.Status == BookIssueStatus.Returned)
		{
			dto.IsOverdue = false;
			return;
		}
		var due = LibraryLoanDates.ConfirmedDueDate(loan);
		if (loan.Status != BookIssueStatus.Issued || !due.HasValue || !asOf.Date.HasValue)
		{
			dto.OverdueUnavailableReason = loan.Status != BookIssueStatus.Issued ? "Review the recorded loan status before classifying it." :
				!due.HasValue ? "The original school due date needs review before overdue classification." : asOf.UnavailableReason;
			return;
		}
		dto.IsOverdue = due.Value < asOf.Date.Value;
		if (dto.IsOverdue == true) dto.Status = (int)BookIssueStatus.Overdue;
	}

	private sealed record ReadAsOfEvidence(DateOnly? Date, string Source, string? UnavailableReason);
}
