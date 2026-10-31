using System;

namespace ShikshaMitra.Library.IssueReturn;

public class IssueReturnItemDto
{
    public Guid TransactionId { get; set; }
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
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public int Status { get; set; }
}
