using System;

namespace ShikshaMitra.Finance.Events;

public class FeeRefundApprovedEto
{
    public Guid RefundRequestId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? FinanceFeeAccountId { get; set; }
    public Guid AcademicYearId { get; set; }
    public double TotalRefundAmount { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime ApprovedAt { get; set; }
}
