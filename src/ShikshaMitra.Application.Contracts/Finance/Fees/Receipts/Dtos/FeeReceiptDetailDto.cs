using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Receipts.Dtos;

/// <summary>
/// Full receipt detail returned by <c>GET api/v1/fee-receipts/{id}</c>.
/// </summary>
public class FeeReceiptDetailDto
{
    public Guid Id { get; set; }

    // ── Document identity ─────────────────────────────────────────────────────

    public string ReceiptNumber { get; set; } = string.Empty;
    public FeeReceiptKind ReceiptKind { get; set; }
    public string ReceiptKindLabel { get; set; } = string.Empty;
    public string PaymentMethodLabel { get; set; } = string.Empty;
    /// <summary>Original collection evidence, shared by provisional and official receipts.</summary>
    public List<FeeReceiptPaymentInstrumentDto> PaymentInstruments { get; set; } = [];
    public string PdfFileName { get; set; } = string.Empty;
    public FeeReceiptScope ReceiptScope { get; set; }

    // ── Verification ──────────────────────────────────────────────────────────

    public FeeReceiptVerificationStatus VerificationStatus { get; set; }
    public Guid? LinkedProvisionalReceiptId { get; set; }
    public string? LinkedProvisionalReceiptNumber { get; set; }
    public Guid? LinkedFinalReceiptId { get; set; }
    public string? LinkedFinalReceiptNumber { get; set; }

    // ── Amounts ───────────────────────────────────────────────────────────────

    public double TotalPaidAmount { get; set; }
	public decimal FeePaidAmount { get; set; }
	public decimal ChargePaidAmount { get; set; }

    // ── Student ───────────────────────────────────────────────────────────────

    public Guid? StudentId { get; set; }
    public Guid? FinanceFeeAccountId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int? GrNo { get; set; }
    public string? RollNo { get; set; }
    public string? ClassName { get; set; }
    public string? Division { get; set; }

    // ── Academic context ─────────────────────────────────────────────────────

    public Guid AcademicYearId { get; set; }
    public Guid BoardId { get; set; }
    public Guid EducationMediumId { get; set; }
    public string? AcademicYearName { get; set; }
    public DateTime? AcademicYearStartDate { get; set; }
    public DateTime? AcademicYearEndDate { get; set; }
    public string? BoardName { get; set; }
    public string? MediumName { get; set; }

    // ── Dates ─────────────────────────────────────────────────────────────────

    public DateOnly ReceiptDate { get; set; }
    public DateTime CreationTime { get; set; }

    // ── PDF / download ────────────────────────────────────────────────────────

    public bool HasPdf { get; set; }

    // ── Transaction correlation ───────────────────────────────────────────────

    public Guid FeeCollectionTransactionId { get; set; }

    /// <summary>
    /// Receipt number of the sibling receipt from the same visit (fee ↔ charge).
    /// </summary>
    public string? SiblingReceiptNumber { get; set; }

    public string? Remarks { get; set; }

    /// <summary>Paid fee-head rollup for display (fee receipts only).</summary>
    public List<FeeReceiptPaidHeadDto> PaidFeeHeads { get; set; } = new();

    /// <summary>Per-installment allocation lines snapped at collect time.</summary>
    public List<FeeReceiptAllocationLineDto> AllocationDetails { get; set; } = new();
}

public class FeeReceiptPaidHeadDto
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public double AmountPaid { get; set; }
}

public class FeeReceiptAllocationLineDto
{
    public Guid DueFeeId { get; set; }
    public Guid? FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public string InstallmentLabel { get; set; } = string.Empty;
    public DateTime? CoverageStartDate { get; set; }
    public DateTime? CoverageEndDate { get; set; }
    public DateTime? DueDate { get; set; }
    public double AmountPaid { get; set; }
    public string ScheduleSource { get; set; } = "Legacy";
}
