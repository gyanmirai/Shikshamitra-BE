using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Library.Policies;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.IssueReturn;

public interface ILibraryIssuePlanAppService : IApplicationService
{
	Task<LibraryIssuePlanDto> PreviewAsync(LibraryIssuePlanRequest input);
}

public class LibraryIssuePlanRequest
{
	public Guid CopyId { get; set; }
	public Guid MemberId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public DateOnly IssueDate { get; set; }
	public DateOnly? RequestedDueDate { get; set; }
}

public class LibraryIssuePlanDto
{
	public Guid BookId { get; set; }
	public Guid CopyId { get; set; }
	public Guid MemberId { get; set; }
	public int Role { get; set; }
	public Guid AcademicYearId { get; set; }
	public DateOnly IssueDate { get; set; }
	public DateOnly? DueDate { get; set; }
	public string? ConfirmedSchoolTimeZoneId { get; set; }
	public LibraryBorrowingPolicySnapshotDto Policy { get; set; } = new();
	public int ActiveLoanCount { get; set; }
	public bool HasOpenLoan { get; set; }
	public bool CanIssue { get; set; }
	public List<string> BlockedReasons { get; set; } = [];
	public string Revision { get; set; } = string.Empty;
}
