using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;

// ── List ─────────────────────────────────────────────────────────────────────

/// <summary>
/// A single row in the reconciliation inbox table.
/// Represents one provisional fee receipt that requires staff verification.
/// </summary>
public class ReconciliationListItemDto
{
    public Guid Id { get; set; }

	/// <summary>Provisional receipt number (KFE-*).</summary>
	public Guid? ProvisionalReceiptId { get; set; }
	public string ReceiptNumber { get; set; } = string.Empty;

    // ── Student ──────────────────────────────────────────────────────────
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int? GrNumber { get; set; }
    public int? RollNumber { get; set; }
    public string Board { get; set; } = string.Empty;
    public string Medium { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;

    // ── Payment ───────────────────────────────────────────────────────────
    public Guid FeeCollectionTransactionId { get; set; }
    public PaymentMethodEnum PaymentMethod { get; set; }
    public string PaymentMethodLabel { get; set; } = string.Empty;
    public double Amount { get; set; }

    /// <summary>Instrument-specific details (cheque no., UPI ID, transaction ref, etc.).</summary>
    public Dictionary<string, string?> InstrumentDetails { get; set; } = [];

    // ── Status ────────────────────────────────────────────────────────────

    /// <summary>0=Pending, 1=Approved, 2=Rejected — matches FE PaymentMethodsStatusType enum.</summary>
    public int Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;

    public FeeReceiptVerificationStatus VerificationStatus { get; set; }

	public Guid? FinalReceiptId { get; set; }

	/// <summary>Final receipt number (FIN-*) once approved. Null while pending.</summary>
	public string? FinalReceiptNumber { get; set; }

    public Guid? ChargeReceiptId { get; set; }

    // ── Dates ─────────────────────────────────────────────────────────────
    public DateOnly ReceiptDate { get; set; }

    // ── Acted-by (populated after approve / reject) ───────────────────────
    public Guid? ActedBy { get; set; }
    public string? ActedByName { get; set; }
    public DateTimeOffset? ActedOn { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>
/// Paged list response, mirrors Finance Charges pattern with embedded tab counts.
/// </summary>
public class ReconciliationListResponse
{
    public bool Success { get; set; } = true;
    public List<ReconciliationListItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public ReconciliationTabCountsDto TabCounts { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Per-payment-method pending/approved/rejected counts for tab badge display.
/// One grouped query per request — not 7 round-trips.
/// </summary>
public class ReconciliationTabCountsDto
{
    public int TotalPending { get; set; }
    public int ChequePending { get; set; }
    public int ChequeApproved { get; set; }
    public int ChequeRejected { get; set; }
    public int DemandDraftPending { get; set; }
    public int DemandDraftApproved { get; set; }
    public int DemandDraftRejected { get; set; }
    public int BankTransferPending { get; set; }
    public int BankTransferApproved { get; set; }
    public int BankTransferRejected { get; set; }
    public int CreditCardPending { get; set; }
    public int CreditCardApproved { get; set; }
    public int CreditCardRejected { get; set; }
	public int DebitCardPending { get; set; }
	public int DebitCardApproved { get; set; }
	public int DebitCardRejected { get; set; }
	public int UpiPending { get; set; }
    public int UpiApproved { get; set; }
    public int UpiRejected { get; set; }
}

// ── Actions ───────────────────────────────────────────────────────────────────

public class ApproveReconciliationInput
{
    public string? Remarks { get; set; }
}

public class RejectReconciliationInput
{
    public string Remarks { get; set; } = string.Empty;
}

/// <summary>Returned after approve or reject, mirroring HR LeaveActionResultDto.</summary>
public class ReconciliationActionResponse
{
    public bool Success { get; set; }
    public ReconciliationActionData? Data { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ReconciliationActionData
{
    public Guid ReceiptId { get; set; }
    public FeeReceiptVerificationStatus VerificationStatus { get; set; }

    /// <summary>0=Pending, 1=Approved, 2=Rejected.</summary>
    public int Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;

    /// <summary>Final receipt number (FIN-*) — populated on approve.</summary>
    public string? FinalReceiptNumber { get; set; }
    public Guid ActedBy { get; set; }
    public string ActedByName { get; set; } = string.Empty;
    public DateTimeOffset ActedOn { get; set; }
    public string? Remarks { get; set; }
}

// ── Query input ───────────────────────────────────────────────────────────────

public class GetReconciliationListInput
{
    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Optional month anchor (1–12). When provided, filters by receipt month.
    /// When omitted, all months in the academic year are returned.
    /// </summary>
    public int? Month { get; set; }

    /// <summary>Optional year (four-digit). When omitted, uses <see cref="AcademicYearId"/> span.</summary>
    public int? Year { get; set; }

    /// <summary>
    /// Payment method tab filter.
    /// Maps from FE enum string (e.g. "Cheque", "DemandDraft", "BankTransfer", "CreditCard",
    /// "UPI", "OnlineGateway", "Wallet") to <see cref="PaymentMethodEnum"/>.
    /// Null = all methods.
    /// </summary>
    public PaymentMethodEnum? PaymentMethod { get; set; }

    /// <summary>Optional verification status filter. Null = all statuses (Pending by default in UI).</summary>
    public FeeReceiptVerificationStatus? VerificationStatus { get; set; }

    /// <summary>Free-text search across student name, GR number, receipt number.</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
