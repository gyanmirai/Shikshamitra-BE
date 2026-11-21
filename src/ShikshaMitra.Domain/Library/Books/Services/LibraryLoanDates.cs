using System;
using System.Linq.Expressions;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Books;

public static class LibraryLoanDates
{
	public static bool IsConfirmed(LibraryCivilDateProvenance provenance) =>
		provenance is LibraryCivilDateProvenance.ExplicitSchoolDate or LibraryCivilDateProvenance.ReviewedLegacy;

	public static DateOnly? ConfirmedIssueDate(BookIssue loan) => IsConfirmed(loan.IssueDateProvenance) ? loan.SchoolIssueDate : null;
	public static DateOnly? ConfirmedDueDate(BookIssue loan) => IsConfirmed(loan.DueDateProvenance) ? loan.SchoolDueDate : null;
	public static DateOnly? ConfirmedReturnDate(BookIssue loan) => IsConfirmed(loan.ReturnDateProvenance) ? loan.SchoolReturnDate : null;

	public static DateOnly? DisplayIssueDate(BookIssue loan) => loan.SchoolIssueDate ?? StoredComponent(loan.IssueDate);
	public static DateOnly? DisplayDueDate(BookIssue loan) => loan.SchoolDueDate ?? StoredComponent(loan.DueDate);
	public static DateOnly? DisplayReturnDate(BookIssue loan) => loan.SchoolReturnDate ?? StoredComponent(loan.ReturnDate);

	public static Expression<Func<BookIssue, bool>> DueOn(DateOnly date) => loan =>
		loan.SchoolDueDate == date && (loan.DueDateProvenance == LibraryCivilDateProvenance.ExplicitSchoolDate ||
		loan.DueDateProvenance == LibraryCivilDateProvenance.ReviewedLegacy);

	public static Expression<Func<BookIssue, bool>> DueBefore(DateOnly date) => loan =>
		loan.SchoolDueDate < date && (loan.DueDateProvenance == LibraryCivilDateProvenance.ExplicitSchoolDate ||
		loan.DueDateProvenance == LibraryCivilDateProvenance.ReviewedLegacy);

	private static DateOnly? StoredComponent(DateTime? raw) => raw.HasValue ? DateOnly.FromDateTime(raw.Value) : null;
}
