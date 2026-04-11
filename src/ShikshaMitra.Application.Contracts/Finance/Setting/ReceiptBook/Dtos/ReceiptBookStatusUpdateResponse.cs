using System;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ReceiptBook.Dtos;

public class ReceiptBookStatusUpdateResponse
{
    public Guid Id { get; set; }
    public bool PreviousStatus { get; set; }
    public bool CurrentStatus { get; set; }
    public DateTime UpdatedAt { get; set; }
}
