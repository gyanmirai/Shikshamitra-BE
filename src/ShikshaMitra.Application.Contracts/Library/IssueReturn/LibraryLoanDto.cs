using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ShikshaMitra.Library.Policies;

namespace ShikshaMitra.Library.IssueReturn;

public class LibraryLoanDto
{
	public Guid Id { get; set; }
	public Guid CopyId { get; set; }
	public Guid? BookId { get; set; }
	public Guid MemberId { get; set; }
	public Guid? CanonicalMemberId { get; set; }
	public int Role { get; set; }
	public Guid? AcademicYearId { get; set; }
	public Guid? BorrowerScopeCaptureId { get; set; }
	public int? BorrowerScopeCaptureVersion { get; set; }
	public int BorrowerScopeProvenance { get; set; }
	public int Status { get; set; }
	public DateOnly? SchoolIssueDate { get; set; }
	public DateOnly? SchoolDueDate { get; set; }
	public DateOnly? OriginalSchoolDueDate { get; set; }
	public DateOnly? SchoolReturnDate { get; set; }
	public int IssueDateProvenance { get; set; }
	public int DueDateProvenance { get; set; }
	public int ReturnDateProvenance { get; set; }
	public DateTime? RawIssueDate { get; set; }
	public DateTime? RawDueDate { get; set; }
	public DateTime? RawReturnDate { get; set; }
	public Guid? IssuedByUserId { get; set; }
	public int RenewalCount { get; set; }
	public LibraryBorrowingPolicySnapshotDto? Policy { get; set; }
	public string Revision { get; set; } = string.Empty;
	public bool CanReturn { get; set; }
	public bool CanRenew { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public LibraryCirculationSchoolContextDto? SchoolContext { get; set; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? CanReviewDates { get; set; }
	public List<string> BlockedReasons { get; set; } = [];
}

public class LibraryLoanCommandResultDto
{
	public LibraryLoanDto Loan { get; set; } = new();
	public bool WasReplay { get; set; }
	public List<Guid> AssessmentIds { get; set; } = [];
}
