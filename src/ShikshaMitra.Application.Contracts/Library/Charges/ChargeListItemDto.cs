using System;

namespace ShikshaMitra.Library.Charges;

public class ChargeListItemDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; }
    public int Role { get; set; }
    public string ChargeType { get; set; }
    public decimal Amount { get; set; }
    public DateTime ChargeDate { get; set; }
    public DateTime DueDate { get; set; }
    public int PaymentStatus { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? BookTitle { get; set; }
    public string? AccessionNo { get; set; }
}
