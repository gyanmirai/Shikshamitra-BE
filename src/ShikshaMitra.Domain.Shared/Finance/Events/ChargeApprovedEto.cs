using System;

namespace ShikshaMitra.Finance.Events;

public class ChargeApprovedEto
{
    public Guid ChargeId { get; set; }
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public decimal Amount { get; set; }
    public string? ChargeName { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime ApprovedAt { get; set; }
}
