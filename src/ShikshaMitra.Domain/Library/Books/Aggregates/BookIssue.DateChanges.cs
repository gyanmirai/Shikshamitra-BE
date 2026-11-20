using System;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Books;

public partial class BookIssue
{
	public bool HasReviewableSchoolDates() =>
		IssueDateProvenance != LibraryCivilDateProvenance.ExplicitSchoolDate ||
		DueDateProvenance != LibraryCivilDateProvenance.ExplicitSchoolDate ||
		!OriginalSchoolDueDate.HasValue ||
		Status == BookIssueStatus.Returned && ReturnDateProvenance != LibraryCivilDateProvenance.ExplicitSchoolDate;

	public void ReviewSchoolDates(DateOnly? issue, DateOnly? due, DateOnly? originalDue, DateOnly? returned)
	{
		if (!issue.HasValue && !due.HasValue && !originalDue.HasValue && !returned.HasValue)
			throw new BusinessException(LibraryCirculationErrorCodes.DateReviewRequired);
		if (issue == default(DateOnly) || due == default(DateOnly) || originalDue == default(DateOnly) || returned == default(DateOnly) ||
			issue.HasValue && IssueDateProvenance == LibraryCivilDateProvenance.ExplicitSchoolDate ||
			due.HasValue && DueDateProvenance == LibraryCivilDateProvenance.ExplicitSchoolDate ||
			returned.HasValue && ReturnDateProvenance == LibraryCivilDateProvenance.ExplicitSchoolDate ||
			originalDue.HasValue && OriginalSchoolDueDate.HasValue || returned.HasValue && Status != BookIssueStatus.Returned)
			throw new BusinessException(LibraryCirculationErrorCodes.DateReviewRequired,
				"Review uncertain legacy dates here. Confirmed transaction dates require an approved correction.");
		var nextIssue = issue ?? LibraryLoanDates.ConfirmedIssueDate(this);
		var nextDue = due ?? LibraryLoanDates.ConfirmedDueDate(this);
		var nextReturn = returned ?? LibraryLoanDates.ConfirmedReturnDate(this);
		if (nextIssue.HasValue && (nextDue < nextIssue || originalDue < nextIssue || nextReturn < nextIssue) ||
			originalDue.HasValue && nextDue.HasValue && originalDue > nextDue)
			throw new BusinessException(LibraryErrorCodes.InvalidIssueDateRange);
		if (issue.HasValue) { SchoolIssueDate = issue; IssueDateProvenance = LibraryCivilDateProvenance.ReviewedLegacy; }
		if (due.HasValue) { SchoolDueDate = due; DueDateProvenance = LibraryCivilDateProvenance.ReviewedLegacy; }
		if (returned.HasValue) { SchoolReturnDate = returned; ReturnDateProvenance = LibraryCivilDateProvenance.ReviewedLegacy; }
		if (originalDue.HasValue) OriginalSchoolDueDate = originalDue;
	}

	public void ApplyRenewedDueDate(DateOnly due)
	{
		var previous = LibraryLoanDates.ConfirmedDueDate(this);
		if (Status != BookIssueStatus.Issued || !previous.HasValue || due == default || due <= previous.Value ||
			string.IsNullOrWhiteSpace(PolicySnapshotJson))
			throw new BusinessException(LibraryCirculationErrorCodes.RenewalNotAllowed);
		SchoolDueDate = due;
		DueDateProvenance = LibraryCivilDateProvenance.ExplicitSchoolDate;
		RenewalCount = checked(RenewalCount + 1);
	}

	public void ReceivePhysicalCopy(DateOnly returned, bool reviewedUnknownDates)
	{
		if (Status != BookIssueStatus.Issued) throw new BusinessException(LibraryErrorCodes.ActiveIssueNotFound);
		if (returned == default) throw new BusinessException(LibraryErrorCodes.InvalidReturnDate);
		var issued = LibraryLoanDates.ConfirmedIssueDate(this);
		if (issued.HasValue && returned < issued.Value) throw new BusinessException(LibraryErrorCodes.InvalidReturnDate);
		if ((!issued.HasValue || !LibraryLoanDates.ConfirmedDueDate(this).HasValue) && !reviewedUnknownDates)
			throw new BusinessException(LibraryCirculationErrorCodes.DateReviewRequired);
		SchoolReturnDate = returned;
		ReturnDateProvenance = LibraryCivilDateProvenance.ExplicitSchoolDate;
		Status = BookIssueStatus.Returned;
	}
}
