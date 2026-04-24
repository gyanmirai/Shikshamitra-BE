using System;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class ReceiptBookStatusUpdateResponse
{
    public Guid Id { get; set; }
    public bool PreviousStatus { get; set; }
    public bool CurrentStatus { get; set; }
    public DateTime UpdatedAt { get; set; }
}
