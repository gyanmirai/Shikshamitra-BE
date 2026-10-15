using System;

namespace ShikshaMitra.Finance.Fees.Receipts.Dtos;

/// <summary>
/// One row in the paginated receipt list.
/// </summary>
public class FeeReceiptListItemDto
{
    public Guid Id { get; set; }

    // ── Document identity ─────────────────────────────────────────────────────

    /// <summary>Clickable receipt number (links to preview/download).</summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>Receipt kind — drives the "Receipt Type" column badge.</summary>
    public FeeReceiptKind ReceiptKind { get; set; }

    /// <summary>UI label: "Final Receipt" / "Kachi Receipt (Fee)" / "Kachi Receipt (Charge)".</summary>
    public string ReceiptKindLabel { get; set; } = string.Empty;

    /// <summary>Scope of this receipt (Fee or Charge).</summary>
    public FeeReceiptScope ReceiptScope { get; set; }

    // ── Verification ──────────────────────────────────────────────────────────

    public FeeReceiptVerificationStatus VerificationStatus { get; set; }

    /// <summary>Receipt number of the linked final receipt (for ProvisionalFee once verified).</summary>
    public string? LinkedFinalReceiptNumber { get; set; }

    // ── Amounts ───────────────────────────────────────────────────────────────

    /// <summary>Total paid amount for this receipt's scope.</summary>
    public double TotalPaidAmount { get; set; }

    // ── Student ───────────────────────────────────────────────────────────────

    public Guid? StudentId { get; set; }
    public Guid? FinanceFeeAccountId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int? GrNo { get; set; }
    public string? RollNo { get; set; }

    // ── Dates ─────────────────────────────────────────────────────────────────

    public DateOnly ReceiptDate { get; set; }
    public DateTime CreationTime { get; set; }

    // ── PDF / download ────────────────────────────────────────────────────────

    /// <summary>True when a PDF has been generated and stored; drives download button state.</summary>
    public bool HasPdf { get; set; }

    // ── Sibling correlation ───────────────────────────────────────────────────

    /// <summary>
    /// When a fee receipt and a charge receipt were created for the same visit,
    /// both share this transaction ID. Lets the UI show a "Related receipt" link.
    /// </summary>
    public Guid FeeCollectionTransactionId { get; set; }
}
