using System;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.Events;

public class FeeReconciliationRejectedEto
{
    public Guid ProvisionalReceiptId { get; set; }
    public string ProvisionalReceiptNumber { get; set; } = default!;
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public FeeReceiptKind ReceiptKind { get; set; }
    public string Remarks { get; set; } = default!;
    public Guid? TenantId { get; set; }
    public DateTime RejectedAt { get; set; }
}
