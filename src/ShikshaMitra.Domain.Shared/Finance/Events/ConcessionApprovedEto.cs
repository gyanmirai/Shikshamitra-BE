using System;

namespace ShikshaMitra.Finance.Events;

public class ConcessionApprovedEto
{
    public Guid ConcessionRequestId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ConcessionTypeId { get; set; }
    public decimal TotalConcessionAmount { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime ApprovedAt { get; set; }
}
