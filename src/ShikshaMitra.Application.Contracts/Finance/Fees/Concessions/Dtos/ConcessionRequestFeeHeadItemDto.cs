using System;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// A single fee-head entry within a student concession-request list item.
/// </summary>
public class ConcessionRequestFeeHeadItemDto
{
    public Guid FeeHeadId { get; set; }

    public string FeeHeadName { get; set; } = string.Empty;

    /// <summary>Original (pre-concession) fee amount for this head.</summary>
    public decimal Amount { get; set; }

    /// <summary>Net amount the student must pay after the concession is applied.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Concession discount amount granted for this fee head.</summary>
    public decimal PendingAmount { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Status integer (0 by default; no per-fee-head lifecycle in the current model).</summary>
    public int Status { get; set; }
}
