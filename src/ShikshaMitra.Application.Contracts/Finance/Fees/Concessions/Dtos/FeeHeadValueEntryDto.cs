using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

/// <summary>
/// Discount value entry for a single fee head (Step 4).
/// </summary>
public class FeeHeadValueEntryDto
{
    /// <summary>ID of the fee head (must have been mapped in Step 3).</summary>
    [Required]
    public Guid FeeHeadId { get; set; }

    /// <summary>
    /// Discount mode discriminator.
    /// <list type="bullet">
    ///   <item><description><b>0</b> — AMOUNT_BASED: <c>discountAmount = amount</c></description></item>
    ///   <item><description><b>1</b> — PERCENTAGE: <c>discountAmount = (amount / 100) × annualBase</c></description></item>
    /// </list>
    /// </summary>
    [Range(0, 1, ErrorMessage = "basedOn must be 0 (AMOUNT_BASED) or 1 (PERCENTAGE).")]
    public int BasedOn { get; set; }

    /// <summary>
    /// The user-specified discount value.
    /// <list type="bullet">
    ///   <item><description>For PERCENTAGE: must be in [0, 100].</description></item>
    ///   <item><description>For AMOUNT_BASED: must be in [0, <see cref="AnnualBaseAmount"/>].</description></item>
    /// </list>
    /// Zero is valid and means no discount is applied.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "amount must be ≥ 0.")]
    public decimal Amount { get; set; }

    /// <summary>
    /// The fee head's annual base amount before concession — sourced from the student's
    /// fee structure by the caller. The server uses this value to compute
    /// <c>discountAmount</c> and <c>netAmount</c> (post-concession amount).
    /// Must be ≥ 0.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "annualBaseAmount must be ≥ 0.")]
    public decimal AnnualBaseAmount { get; set; }
}
