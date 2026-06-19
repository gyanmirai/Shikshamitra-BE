using System;

namespace ShikshaMitra.Finance.Events;

public class FeeAdjustmentAppliedEto
{
    public Guid AdjustmentRequestId { get; set; }
    public string VoucherNumber { get; set; } = default!;
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public double TotalAdjustedAmount { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AppliedAt { get; set; }
}
