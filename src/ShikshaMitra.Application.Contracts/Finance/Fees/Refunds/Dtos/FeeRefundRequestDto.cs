using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

/// <summary>Response DTO for a single <see cref="FeeRefundRequest"/>.</summary>
public class FeeRefundRequestDto
{
    public Guid Id { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;

    public Guid? StudentId { get; set; }
    public Guid? FinanceFeeAccountId { get; set; }

    public Guid AcademicYearId { get; set; }
    public string? AcademicYearName { get; set; }
    public DateTime? AcademicYearStartDate { get; set; }
    public DateTime? AcademicYearEndDate { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }

    public FeeRefundStatus Status { get; set; }
    public int WorkflowVersion { get; set; }
    public Guid? ReplacesRefundRequestId { get; set; }
    public Guid? ReplacedByRefundRequestId { get; set; }
	public Guid? StudentFeeStructureId { get; set; }
	public Guid? RefundPolicyId { get; set; }
	public Guid? RefundPolicyVersionId { get; set; }
	public Guid? ConcessionRequestId { get; set; }
	public string? RefundPolicyName { get; set; }
	public Guid? OriginalReceiptId { get; set; }
	public string? OriginalReceiptNumber { get; set; }
	public string Currency { get; set; } = "INR";

    public string VoucherNumber { get; set; } = default!;

    public decimal TotalAmount { get; set; }
    public decimal CalculatedAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal ProcessedAmount { get; set; }
    public decimal ReversedAmount { get; set; }
    /// <summary>Net amount posted to the refund subledger for this request.</summary>
    public decimal? RefundSubledgerNetAmount { get; set; }
    /// <summary>Reconciles the request, lines, due fees and immutable refund postings.</summary>
    public bool? IsRefundSubledgerBalanced { get; set; }

    public Guid? ExpenseCategoryId { get; set; }
    public Guid? PayingBankAccountId { get; set; }
    public Guid? DefaultExpenseCategoryId { get; set; }
    public Guid? DefaultPayingBankAccountId { get; set; }
    public string? ExpenseCategoryName { get; set; }
    public string? PayingBankLabel { get; set; }
    public string? RefundMethod { get; set; }
    /// <summary>Payment method labels allowed by the request's pinned refund policy version.</summary>
    public List<string> AllowedPayoutMethods { get; set; } = [];

    /// <summary>
    /// Method-specific disbursement details returned as a flat key/value map,
    /// mirroring the <c>paymentDetails</c> object the frontend sent on creation.
    /// Keys depend on the <see cref="RefundMethod"/> (e.g., "chequeNumber", "upiId").
    /// </summary>
    public Dictionary<string, object?>? PaymentDetails { get; set; }

    public string? Reason { get; set; }

    public string? ApproverNotes { get; set; }

    public Guid? ProcessedByUserId { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset CreationTime { get; set; }
	public DateTimeOffset? SubmittedAt { get; set; }
	public DateTimeOffset? ReviewStartedAt { get; set; }
	public DateTimeOffset? ReversedAt { get; set; }
	public string? FailureReason { get; set; }
	public string? ReversalReason { get; set; }
	public List<string> AvailableActions { get; set; } = [];
	public List<FeeRefundSettlementDto> Settlements { get; set; } = [];

    public List<FeeRefundLineDto> Lines { get; set; } = [];

    // ── Enhanced Response Fields for Refund Request View Screen ──

    public Guid RefundRequestId { get; set; }

    public int TotalFeeHeads { get; set; }

    public double TotalPaidAmount { get; set; }

    public double TotalRefundedAmount { get; set; }

    public List<RefundFeeHeadDto> FeeHeads { get; set; } = [];

    // ── Denormalized student snapshot (populated by the list / get endpoints) ──

    /// <summary>Full display name: FirstName [MiddleName] LastName.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>School-assigned GR number.</summary>
    public int? StudentGrNo { get; set; }

    /// <summary>Roll number within the division.</summary>
    public int? StudentRollNo { get; set; }

    /// <summary>Standard / class name (e.g. "Class 5", "Nursery").</summary>
    public string? StandardName { get; set; }

    /// <summary>Division name (e.g. "A").</summary>
    public string? DivisionName { get; set; }

    /// <summary>Student's primary contact number.</summary>
    public string? StudentContactNo { get; set; }

    public Guid? BoardId { get; set; }

    public string? BoardName { get; set; }

    public Guid? EducationMediumId { get; set; }

    public string? EducationMediumName { get; set; }
}

/// <summary>Response DTO for a single refund line.</summary>
public class FeeRefundLineDto
{
    public Guid Id { get; set; }

    public Guid? ChargeId { get; set; }
    /// <summary>Due line whose collected late fee is being refunded; distinct from principal.</summary>
    public Guid? LateFeeDueFeeId { get; set; }
    public Guid? DueFeeId { get; set; }

    public Guid? SourceAllocationId { get; set; }
    public Guid? SourceReceiptId { get; set; }
    public Guid? FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public string InstallmentLabel { get; set; } = string.Empty;
    public decimal SettledPaidAmount { get; set; }
    public decimal Amount { get; set; }
    public decimal CalculatedAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal ProcessedAmount { get; set; }
    public decimal ReversedAmount { get; set; }
}

public class FeeRefundSettlementDto
{
    public Guid Id { get; set; }
    public int AttemptNumber { get; set; }
    public FeeRefundSettlementStatus Status { get; set; }
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset SettlementDate { get; set; }
    public string? ExternalReference { get; set; }
    public string? FailureReason { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? EvidenceReference { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}

public class RefundFeeHeadDto
{
    public Guid? FeeHeadId { get; set; }

    public string FeeHeadName { get; set; } = string.Empty;

    public double TotalPaid { get; set; }

    public double TotalRefunded { get; set; }

    public List<RefundMonthItemDto> Items { get; set; } = [];
}

public class RefundMonthItemDto
{
    public Guid InstallmentId { get; set; }

    public string Month { get; set; } = string.Empty;

    public double Total { get; set; }

    public double Paid { get; set; }

    public double RefundAmount { get; set; }

    public int Status { get; set; }
}

