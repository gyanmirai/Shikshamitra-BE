using System;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Previously saved discount value for a single fee head (Step 4 GET response).
/// Matches the shape read by <c>values.valueEntries[concessionTypeId][feeHeadId]</c>
/// in the Step-4 UI and the payload shape sent by the Step-4 PUT mutation.
/// </summary>
public class SavedFeeHeadValueDto
{
    /// <summary>Fee head UUID.</summary>
    public Guid FeeHeadId { get; set; }

    /// <summary>
    /// Discount mode. 0 = Flat amount (₹); 1 = Percentage (%).
    /// </summary>
    public int BasedOn { get; set; }

    /// <summary>
    /// User-specified discount — flat rupee amount when <see cref="BasedOn"/> is 0,
    /// percentage (0–100) when <see cref="BasedOn"/> is 1.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>Post-concession fee head amount (annual base minus computed discount).</summary>
    public decimal NetAmount { get; set; }
}
