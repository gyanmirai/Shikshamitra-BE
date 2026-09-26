using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Collections.Dtos;

/// <summary>
/// Complete fee information for a student in a given academic year,
/// including fee heads breakdown, payment history, and applied concessions.
/// </summary>
public class StudentFeeDetailsDto
{
    public StudentSummaryDto Student { get; set; } = default!;
    public FeeOverviewDto FeeOverview { get; set; } = default!;
    public List<FeeHeadSummaryDto> FeeHeads { get; set; } = [];
    public List<FeeChargeDetailDto>? Charges { get; set; }
    public List<FeePaymentHistoryDto>? Payments { get; set; }
    public List<ConcessionDetailDto>? Concessions { get; set; }
}

/// <summary>Lightweight student profile embedded in the fee details response.</summary>
public class StudentSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StandardName { get; set; } = string.Empty;
    public string DivisionName { get; set; } = string.Empty;
    public string RollNo { get; set; } = string.Empty;
    public string? MobileNo { get; set; }
}

/// <summary>Aggregated financial overview for the academic year.</summary>
public class FeeOverviewDto
{
    public double TotalFee { get; set; }
    public double PaidFee { get; set; }
    public double PendingFee { get; set; }
    public double? ConcessionAmount { get; set; }

    /// <summary>
    /// Sum of outstanding amounts for fee periods that have already passed the current
    /// month. Null when the student has no overdue obligations.
    /// </summary>
    public double? OverdueFee { get; set; }

    /// <summary>
    /// Sum of all fee-head allocations whose parent <c>FeeRecordMetadata</c> is currently
    /// <c>InReconciliation</c> (cheque/DD submitted but not yet cleared). Zero when none pending.
    /// </summary>
    public double? ReconciliationAmount { get; set; }
}

/// <summary>Per-fee-head payment breakdown.</summary>
public class FeeHeadSummaryDto
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public double Amount { get; set; }
    public double PaidAmount { get; set; }
    public double? PendingAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public FeeCollectionStatus Status { get; set; }

    /// <summary>
    /// Amount for this fee head whose payment is currently <c>InReconciliation</c>
    /// (cheque/DD submitted but not yet cleared). Null or zero when none pending.
    /// </summary>
    public double? ReconciliationAmount { get; set; }
}

/// <summary>Per-charge payment breakdown (matches top-level <c>charges</c> array).</summary>
public class FeeChargeDetailDto
{
    public Guid ChargeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Amount { get; set; }
    public double PaidAmount { get; set; }
    public double PendingAmount { get; set; }
    public double WaivedAmount { get; set; }
    public bool CanWaive { get; set; }
    public FeeCollectionStatus Status { get; set; }
}

/// <summary>Single payment transaction in the payment history.</summary>
public class FeePaymentHistoryDto
{
    public Guid PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public double Amount { get; set; }
    public List<PaymentMethodEnum> PaymentMethods { get; set; } = [];
    public string? ReceiptUrl { get; set; }
    public FeeCollectionStatus Status { get; set; }
}

/// <summary>Applied concession with amount for the student.</summary>
public class ConcessionDetailDto
{
    public Guid ConcessionRequestConcessionTypeId { get; set; }
    public string ConcessionTypeName { get; set; } = string.Empty;
    public double? Amount { get; set; }
}

