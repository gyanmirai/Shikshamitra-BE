using System;
using System.Collections.Generic;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.IssueReturn;

public class LibraryReturnAssessmentCategoryDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public bool IsFixed { get; set; }
}

public class LibraryCopyLookupDto : AccessionBookDto
{
	public Guid CopyId { get; set; }
	public int CopyStatus { get; set; }
	public string? CoverImage { get; set; }
	public LibraryCirculationSchoolContextDto SchoolContext { get; set; } = new();
}

public class LibraryReturnLookupDto
{
	public LibraryCopyLookupDto Copy { get; set; } = new();
	public LibraryLoanDto Loan { get; set; } = new();
	public LibraryLoanLookupBorrowerDto Borrower { get; set; } = new();
	public LibraryCirculationSchoolContextDto SchoolContext { get; set; } = new();
}

public class LibraryCirculationSchoolContextDto
{
	public DateOnly? SchoolToday { get; set; }
	public string? ConfirmedTimeZoneId { get; set; }
	public bool IsConfirmed { get; set; }
	public bool RequiresExplicitDate { get; set; } = true;
	public string? UnavailableReason { get; set; }
	public List<NamedIdDto> AcademicYears { get; set; } = [];
}

public class LibraryLoanLookupBorrowerDto
{
	public Guid? MemberId { get; set; }
	public string? MemberName { get; set; }
	public string? MemberIdentifier { get; set; }
	public int Role { get; set; }
	public string? ClassDivision { get; set; }
	public string DisplaySource { get; set; } = "Unavailable";
}
