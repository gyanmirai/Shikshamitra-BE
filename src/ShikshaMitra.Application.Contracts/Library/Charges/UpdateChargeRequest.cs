using System;

namespace ShikshaMitra.Library.Charges;

public class UpdateChargeRequest
{
    public decimal? Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public int? PaymentStatus { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? Remarks { get; set; }
}
