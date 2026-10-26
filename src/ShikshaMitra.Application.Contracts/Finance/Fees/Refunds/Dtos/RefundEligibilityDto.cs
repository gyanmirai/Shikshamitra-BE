using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

/// <summary>
/// Summary of a student's refund eligibility for an academic year.
/// Rows identify the original payment allocation and either its fee due or paid charge.
/// </summary>
public class RefundEligibilityDto
{
    public Guid? StudentId { get; set; }
    public Guid? FinanceFeeAccountId { get; set; }

    public Guid AcademicYearId { get; set; }

    /// <summary>Total eligible fee and charge payments.</summary>
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

    /// <summary>Fee-head groups and the separate paid-charge group, with their payment rows.</summary>
    public List<RefundEligibilityFeeHeadDto> FeeHeads { get; set; } = [];
}

/// <summary>Eligibility summary for a fee head or the paid-charge group.</summary>
public class RefundEligibilityFeeHeadDto
{
    /// <summary>Null for the paid-charge group.</summary>
    public Guid? FeeHeadId { get; set; }

    public string FeeHeadName { get; set; } = default!;

    public decimal TotalPaid { get; set; }

    public decimal TotalRefunded { get; set; }

    public decimal TotalRefundable { get; set; }

    /// <summary>Settled payment rows with stable source identifiers.</summary>
    public List<RefundEligibilityMonthDto> Months { get; set; } = [];
}

/// <summary>Eligibility for one settled fee or charge payment allocation.</summary>
public class RefundEligibilityMonthDto
{
    /// <summary>Charge source; mutually exclusive with DueFeeId.</summary>
    public Guid? ChargeId { get; set; }
    /// <summary>Due line whose collected late fee is being refunded; distinct from principal.</summary>
    public Guid? LateFeeDueFeeId { get; set; }
    /// <summary>Fee-due source; mutually exclusive with ChargeId.</summary>
    public Guid? DueFeeId { get; set; }
    public Guid SourceAllocationId { get; set; }
    public Guid SourceReceiptId { get; set; }
    public string? ChargeName { get; set; }
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
    public string? ChargeName { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateOnly ReceiptDate { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundableAmount { get; set; }
}
