using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.IssueReturn;

public class LibraryCheckoutRequest : LibraryIssuePlanRequest
{
	[Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
	[Required, MaxLength(128)] public string Revision { get; set; } = string.Empty;
	[MaxLength(500)] public string? Remarks { get; set; }
}

public class LibraryLoanCommandRequest
{
	[Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
	[Required, MaxLength(128)] public string Revision { get; set; } = string.Empty;
}

public class LibraryLoanReturnRequest : LibraryLoanCommandRequest
{
	public Guid CopyId { get; set; }
	public DateOnly SchoolReturnDate { get; set; }
	public int CopyStatus { get; set; }
	[MaxLength(500)] public string? Remarks { get; set; }
	[MaxLength(500)] public string? UnknownOriginalDatesReason { get; set; }
	public List<LibraryReturnAssessmentRequest> Assessments { get; set; } = [];
}

public class LibraryReturnAssessmentRequest
{
	public Guid ChargeCategoryId { get; set; }
	public Guid? AcademicYearId { get; set; }
	[MaxLength(500)] public string? AcademicYearReason { get; set; }
	public decimal Amount { get; set; }
	public DateOnly SchoolChargeDate { get; set; }
	public DateOnly SchoolDueDate { get; set; }
	[Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
}

public class LibraryLoanRenewRequest : LibraryLoanCommandRequest
{
	public Guid? AcademicYearId { get; set; }
	public DateOnly SchoolRenewalDate { get; set; }
	public DateOnly? RequestedDueDate { get; set; }
	[MaxLength(500)] public string? CalendarChangeReason { get; set; }
	[MaxLength(500)] public string? Reason { get; set; }
}

public class LibraryLoanRenewalPlanRequest
{
	public Guid? AcademicYearId { get; set; }
	public DateOnly SchoolRenewalDate { get; set; }
	public DateOnly? RequestedDueDate { get; set; }
}

public class LibraryLoanDateReviewRequest : LibraryLoanCommandRequest
{
	public DateOnly? SchoolIssueDate { get; set; }
	public DateOnly? SchoolDueDate { get; set; }
	public DateOnly? OriginalSchoolDueDate { get; set; }
	public DateOnly? SchoolReturnDate { get; set; }
	[Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
}
