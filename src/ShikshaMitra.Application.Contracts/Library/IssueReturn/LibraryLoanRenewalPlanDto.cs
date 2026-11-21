using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.IssueReturn;

public class LibraryLoanRenewalPlanDto
{
	public LibraryLoanDto Loan { get; set; } = new();
	public Guid? CurrentAcademicYearId { get; set; }
	public DateOnly SchoolRenewalDate { get; set; }
	public DateOnly? NewSchoolDueDate { get; set; }
	public string? CurrentSchoolTimeZoneId { get; set; }
	public bool RequiresCalendarChangeAcknowledgment { get; set; }
	public bool CanRenew { get; set; }
	public List<string> BlockedReasons { get; set; } = [];
	public string Revision { get; set; } = string.Empty;
}
