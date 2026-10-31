using System;

namespace ShikshaMitra.Library.Charges;

public class ChargeDetailDto
{
    public Guid Id { get; set; }

    public Guid MemberId { get; set; }
    public string? MemberName { get; set; }
    public int Role { get; set; }
    public string? Avatar { get; set; }
    public string? ClassDivision { get; set; }
    public string? RollNo { get; set; }
    public string? GrNo { get; set; }
    public string? ContactNo { get; set; }

    public Guid ChargeCategoryId { get; set; }
    public string ChargeType { get; set; } = null!;

    public Guid? BookIssueId { get; set; }
    public string? BookTitle { get; set; }
    public string? AccessionNo { get; set; }

    public decimal Amount { get; set; }
    public DateTime ChargeDate { get; set; }
    public DateTime DueDate { get; set; }
    public int PaymentStatus { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}
