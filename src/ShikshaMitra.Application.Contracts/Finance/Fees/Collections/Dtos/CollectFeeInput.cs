using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>
/// Input model to record a fee payment for a student.
/// <para>
/// <b>Validation Matrix:</b>
/// <list type="table">
///   <listheader>
///     <term>Scenario</term>
///     <term>dueFeePayments</term>
///     <term>chargePayments</term>
///     <term>Expected</term>
///   </listheader>
///   <item>
///     <description>Full fee only</description>
///     <description>[{ dueFeeId, feeHeadId, amount: 1500 }]</description>
///     <description>[]</description>
///     <description>DueFee fully paid</description>
///   </item>
///   <item>
///     <description>Partial fee</description>
///     <description>[{ dueFeeId, feeHeadId, amount: 500 }] (due was 1500)</description>
///     <description>[]</description>
///     <description>DueFee status Partial</description>
///   </item>
///   <item>
///     <description>Multi-month same head</description>
///     <description>Two entries, same feeHeadId, different dueFeeId</description>
///     <description>[]</description>
///     <description>Two allocations, one FeeCollectionRecord sum</description>
///   </item>
///   <item>
///     <description>Fees + charges</description>
///     <description>One or more due-fee lines</description>
///     <description>One or more charge lines</description>
///     <description>Both persisted</description>
///   </item>
///   <item>
///     <description>Charge only</description>
///     <description>[]</description>
///     <description>[{ chargeId, amount }]</description>
///     <description>Valid</description>
///   </item>
///   <item>
///     <description>Overpay</description>
///     <description>amount > outstanding</description>
///     <description>—</description>
///     <description>400 validation error</description>
///   </item>
///   <item>
///     <description>Payment mismatch</description>
///     <description>lines sum 3000, payments sum 2500</description>
///     <description>—</description>
///     <description>400 validation error</description>
///   </item>
/// </list>
/// </para>
/// </summary>
public class CollectFeeInput
{
    /// <summary>Unique identifier of the student paying fees.</summary>
    [Required]
    public Guid StudentId { get; set; }

    /// <summary>Academic year for which payment is being made.</summary>
    [Required]
    public Guid AcademicYearId { get; set; }

    /// <summary>Optional remarks or notes about the payment.</summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Cashier's confirmation that the payment amount has been received in the bank account.
    /// Required when using cashier-decided payment methods (UPI, cards, transfer, gateway).
    /// </summary>
    public bool? PaymentConfirmed { get; set; }

    /// <summary>Flat list of due fee line-item allocations.</summary>
    public List<DueFeePaymentDto> DueFeePayments { get; set; } = [];

    /// <summary>Flat list of charge payments.</summary>
    public List<ChargePaymentDto> ChargePayments { get; set; } = [];

    /// <summary>
    /// Late fee amount actually being collected per due line — optional, empty by default so
    /// existing callers that don't yet send this are unaffected. The cashier's UI computes
    /// each amount from GET .../collect-form's FeeLedgerLineDto.LateFeeAmount (already net of
    /// any applied waiver), and may reduce it further at collection time. Not required to equal
    /// the computed amount — a cashier can still collect less if a waiver is applied inline.
    /// </summary>
    public List<LateFeePaymentDto> LateFeePayments { get; set; } = [];

    /// <summary>Split payment details by payment method (1–5 items).</summary>
    [Required]
    [MinLength(1)]
    public List<FeePaymentDto> Payments { get; set; } = [];
}

/// <summary>Allocates part of a payment to a specific monthly due fee row.</summary>
public class DueFeePaymentDto
{
    [Required]
    public Guid DueFeeId { get; set; }

    [Required]
    public Guid FeeHeadId { get; set; }

    [Range(0.01, double.MaxValue)]
    public double Amount { get; set; }
}

/// <summary>Late fee amount being collected against a specific due line.</summary>
public class LateFeePaymentDto
{
    [Required]
    public Guid DueFeeId { get; set; }

    [Range(0.01, double.MaxValue)]
    public double Amount { get; set; }
}

/// <summary>An additional charge being paid in this transaction.</summary>
public class ChargePaymentDto
{
    [Required]
    public Guid ChargeId { get; set; }

    [Range(0.01, double.MaxValue)]
    public double Amount { get; set; }
}

/// <summary>A single payment instrument used in this transaction.</summary>
public class FeePaymentDto
{
    [Required]
    public PaymentMethodEnum PaymentMethod { get; set; }

    [Range(0.01, 10_000_000)]
    public double Amount { get; set; }

    /// <summary>Payment method-specific details (e.g., cheque number, UPI reference).</summary>
    public Dictionary<string, object?>? Details { get; set; }
}
