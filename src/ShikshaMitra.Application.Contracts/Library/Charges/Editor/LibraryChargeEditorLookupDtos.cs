using System;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Charges.Editor;

public sealed class LibraryChargeEditorCategoryDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public bool IsFixed { get; set; }
}

public sealed class LibraryChargeEditorMemberDto
{
	public Guid Id { get; set; }
	public string MemberName { get; set; } = string.Empty;
	public string? MemberIdentifier { get; set; }
	public int Role { get; set; }
	public bool HasActiveLibraryMembership { get; set; }
	public bool CanAssignDirect { get; set; }
	public bool RequiresLoanSource { get; set; }
}

public sealed class LibraryChargeEditorLoanSourceDto
{
	public Guid LoanId { get; set; }
	public Guid CanonicalMemberId { get; set; }
	public int Role { get; set; }
	public Guid? CopyId { get; set; }
	public Guid? BookId { get; set; }
	public string AccessionNumber { get; set; } = string.Empty;
	public string? Title { get; set; }
	public Guid? LoanAcademicYearId { get; set; }
	public int Status { get; set; }
	public DateOnly? SchoolIssueDate { get; set; }
	public DateOnly? SchoolDueDate { get; set; }
	public LibraryCivilDateProvenance IssueDateProvenance { get; set; }
	public LibraryCivilDateProvenance DueDateProvenance { get; set; }
}

public sealed class LibraryChargeEditorContextDto
{
	public ChargeDetailDto Assessment { get; set; } = new();
	public bool CanAmend { get; set; }
	public bool RequiresContextReview { get; set; }
	public string? AmendmentBlockedReason { get; set; }
}
