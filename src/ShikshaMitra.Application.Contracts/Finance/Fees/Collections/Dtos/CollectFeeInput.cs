using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ShikshaMitra.Finance.FeeCollection;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>Input model to record a fee payment for a student.</summary>
public class CollectFeeInput
{
    /// <summary>Unique identifier of the student paying fees.</summary>
    [Required]
    public Guid StudentId { get; set; }

    /// <summary>Academic year for which payment is being made.</summary>
    [Required]
    public Guid AcademicYearId { get; set; }

    /// <summary>Total amount being collected (sum of all fee heads and charges).</summary>
    [Range(0.01, 10_000_000)]
    public double TotalAmount { get; set; }

    /// <summary>Actual amount received from payments (may be less than total for partial payments).</summary>
    [Range(0, 10_000_000)]
    public double? ReceivedAmount { get; set; }

    /// <summary>Optional remarks or notes about the payment.</summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>List of fee heads being paid (1–50 items).</summary>
    [Required]
    [MinLength(1)]
    public List<SelectedFeeHeadDto> SelectedFeeHeads { get; set; } = [];

    /// <summary>List of additional charges being paid (0–20 items).</summary>
    public List<SelectedChargeDto>? SelectedCharges { get; set; }

    /// <summary>Split payment details by payment method (1–5 items).</summary>
    [Required]
    [MinLength(1)]
    public List<FeePaymentDto> Payments { get; set; } = [];
}

/// <summary>
/// A fee head being paid in this transaction.
/// When <see cref="Allocations"/> is provided the sum of all allocation amounts must equal
/// <see cref="Amount"/>; each allocation points to a specific <see cref="DueFee"/> row so that
/// monthly balances stay authoritative for future refund eligibility.
/// </summary>
public class SelectedFeeHeadDto
{
    [Required]
    public Guid FeeHeadId { get; set; }

    [Range(0.01, double.MaxValue)]
    public double Amount { get; set; }

    /// <summary>
    /// Optional per-DueFee allocation breakdown.
    /// Leave null / empty for legacy compatibility (no DueFee rows will be updated).
    /// </summary>
    public List<DueFeeAllocationDto>? Allocations { get; set; }
}

/// <summary>Allocates part of a fee-head payment to a specific monthly <see cref="DueFee"/> row.</summary>
public class DueFeeAllocationDto
{
    [Required]
    public Guid DueFeeId { get; set; }

    [Range(0.01, double.MaxValue)]
    public double Amount { get; set; }
}

/// <summary>An additional charge being paid in this transaction.</summary>
public class SelectedChargeDto
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
