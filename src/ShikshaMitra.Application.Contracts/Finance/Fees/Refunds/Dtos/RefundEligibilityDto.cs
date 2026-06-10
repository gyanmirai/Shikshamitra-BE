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
    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    /// <summary>Total amount paid across all fee heads and months.</summary>
    public double TotalPaid { get; set; }

    /// <summary>Total amount already refunded.</summary>
    public double TotalRefunded { get; set; }

    /// <summary>Net refundable balance across all lines.</summary>
    public double TotalRefundable { get; set; }

    /// <summary>Per fee-head groups with their monthly breakdowns.</summary>
    public List<RefundEligibilityFeeHeadDto> FeeHeads { get; set; } = [];
}

/// <summary>Eligibility summary grouped by fee head.</summary>
public class RefundEligibilityFeeHeadDto
{
    public Guid FeeHeadId { get; set; }

    public string FeeHeadName { get; set; } = default!;

    public double TotalPaid { get; set; }

    public double TotalRefunded { get; set; }

    public double TotalRefundable { get; set; }

    /// <summary>Monthly obligation rows — each row carries a stable <see cref="DueFeeId"/>.</summary>
    public List<RefundEligibilityMonthDto> Months { get; set; } = [];
}

/// <summary>Eligibility for one monthly DueFee obligation.</summary>
public class RefundEligibilityMonthDto
{
    /// <summary>The stable DueFee row ID — use this as the <c>DueFeeId</c> when creating refund lines.</summary>
    public Guid DueFeeId { get; set; }

    public MonthEnum Month { get; set; }

    public int Year { get; set; }

    public double Total { get; set; }

    public double Paid { get; set; }

    public double Refunded { get; set; }

    public double Refundable { get; set; }
}
