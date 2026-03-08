using System;

namespace ShikshaMitra.Library.Charges;

public class CreateChargeRequest
{
    public Guid MemberId { get; set; }
    public Guid ChargeCategoryId { get; set; }
    public Guid? BookIssueId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ChargeDate { get; set; }
    public DateTime DueDate { get; set; }
    public string? Remarks { get; set; }
}
