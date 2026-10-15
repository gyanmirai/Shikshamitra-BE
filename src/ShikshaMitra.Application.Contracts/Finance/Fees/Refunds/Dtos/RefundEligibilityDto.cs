using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

/// <summary>
/// Summary of a student's refund eligibility for an academic year.
/// Keyed by <see cref="DueFeeId"/> so the frontend can include stable IDs in create requests.
/// </summary>
public class RefundEligibilityDto
{
    public Guid? StudentId { get; set; }
    public Guid? FinanceFeeAccountId { get; set; }

    public Guid AcademicYearId { get; set; }

    /// <summary>Total amount paid across all fee heads and months.</summary>
    public decimal TotalPaid { get; set; }

    /// <summary>Total amount already refunded.</summary>
    public decimal TotalRefunded { get; set; }

    /// <summary>Net refundable balance across all lines.</summary>
    public decimal TotalRefundable { get; set; }
    public Guid? StudentFeeStructureId { get; set; }
    public Guid? RefundPolicyId { get; set; }
    public Guid? RefundPolicyVersionId { get; set; }
    public string? RefundPolicyName { get; set; }
    public List<string> AllowedPayoutMethods { get; set; } = [];
    public int? ProcessingTargetDays { get; set; }
    public bool AllowPartialRefund { get; set; }
    public List<RefundEligibleReceiptDto> Receipts { get; set; } = [];

    /// <summary>Per fee-head groups with their monthly breakdowns.</summary>
    public List<RefundEligibilityFeeHeadDto> FeeHeads { get; set; } = [];
}

/// <summary>Eligibility summary grouped by fee head.</summary>
public class RefundEligibilityFeeHeadDto
{
    public Guid FeeHeadId { get; set; }

    public string FeeHeadName { get; set; } = default!;

    public decimal TotalPaid { get; set; }

    public decimal TotalRefunded { get; set; }

    public decimal TotalRefundable { get; set; }

    /// <summary>Monthly obligation rows — each row carries a stable <see cref="DueFeeId"/>.</summary>
    public List<RefundEligibilityMonthDto> Months { get; set; } = [];
}

/// <summary>Eligibility for one monthly DueFee obligation.</summary>
public class RefundEligibilityMonthDto
{
    /// <summary>The stable DueFee row ID — use this as the <c>DueFeeId</c> when creating refund lines.</summary>
    public Guid DueFeeId { get; set; }
    public Guid SourceAllocationId { get; set; }
    public Guid SourceReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;

    public MonthEnum Month { get; set; }

    public int Year { get; set; }

    public decimal Total { get; set; }

    public decimal Paid { get; set; }

    public decimal Refunded { get; set; }

    public decimal Refundable { get; set; }
}

public class RefundEligibleReceiptDto
{
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateOnly ReceiptDate { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundableAmount { get; set; }
}
