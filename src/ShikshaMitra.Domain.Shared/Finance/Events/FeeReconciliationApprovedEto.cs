using System;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.Events;

public class FeeReconciliationApprovedEto
{
    public Guid ProvisionalReceiptId { get; set; }
    public Guid FinalReceiptId { get; set; }
    public string FinalReceiptNumber { get; set; } = default!;
    public Guid? StudentId { get; set; }
    public Guid? FinanceFeeAccountId { get; set; }
    public Guid AcademicYearId { get; set; }
    public FeeReceiptKind ReceiptKind { get; set; }
    public double TotalPaidAmount { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime ApprovedAt { get; set; }
}
