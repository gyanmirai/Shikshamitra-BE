using System;

namespace ShikshaMitra.Library.IssueReturn;

public class IssueReturnItemDto
{
    public Guid TransactionId { get; set; }
    public Guid BookId { get; set; }
    public Guid CopyId { get; set; }
    public string? MemberName { get; set; }
    public Guid MemberId { get; set; }
    public int Role { get; set; }
    public string? BookTitle { get; set; }
    public string AccessionNo { get; set; } = null!;
    public string? StandardName { get; set; }
    public string? DivisionName { get; set; }
    public string? RollNo { get; set; }
    public string? GrNo { get; set; }
    public string? ContactNo { get; set; }
    public string? BookCategory { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public int Status { get; set; }
	public int StoredStatus { get; set; }
	public Guid? AcademicYearId { get; set; }
	public DateOnly? SchoolIssueDate { get; set; }
	public DateOnly? SchoolDueDate { get; set; }
	public DateOnly? SchoolReturnDate { get; set; }
	public int IssueDateProvenance { get; set; }
	public int DueDateProvenance { get; set; }
	public int ReturnDateProvenance { get; set; }
	public DateTime? RawIssueDate { get; set; }
	public DateTime? RawDueDate { get; set; }
	public DateTime? RawReturnDate { get; set; }
	public bool? IsOverdue { get; set; }
	public DateOnly? OverdueAsOfDate { get; set; }
	public string OverdueDateSource { get; set; } = "Unavailable";
	public string? OverdueUnavailableReason { get; set; }
	public string? MemberIdentifier { get; set; }
	public string? ClassDivision { get; set; }
	public string MemberDisplaySource { get; set; } = "Unavailable";
}
