using System;

namespace ShikshaMitra.Library.Charges;

public class UpdateChargeRequest
{
    public string? ExpectedSourceVersion { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public DateOnly? SchoolDueDate { get; set; }
    public int? PaymentStatus { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? Remarks { get; set; }
}
