using System;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Data;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// A printable fee or charge receipt document.
/// <para>
/// Collection policy chooses separate fee/charge documents or one combined document.
/// Combined documents retain an immutable fee/charge amount breakdown.
/// </para>
/// <para>
/// The parent transaction is <c>FeeRecordMetadata</c> (acting as <c>FeeCollectionTransaction</c>
/// until the rename sprint). Each receipt holds its own <see cref="ReceiptNumber"/>,
/// <see cref="ReceiptKind"/>, and <see cref="PdfBlobName"/>.
/// </para>
/// </summary>
public class FeePaymentReceipt : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    private FeePaymentReceipt() { /* For ORM */ }

    public FeePaymentReceipt(Guid id) : base(id) { }

    public Guid? TenantId { get; set; }

    // ── Parent transaction ────────────────────────────────────────────────────

    /// <summary>
    /// FK to the parent <c>FeeRecordMetadata</c> (collection transaction).
    /// All receipts from the same counter visit share this id.
    /// </summary>
    public Guid FeeCollectionTransactionId { get; set; }

    // ── Document identity ─────────────────────────────────────────────────────

    /// <summary>
    /// Unique human-readable receipt number.
    /// Format: <c>FIN-{yyyy}-{nnnnnn}</c> for FinalFee,
    ///         <c>KFE-{yyyy}-{nnnnnn}</c> for ProvisionalFee,
    ///         <c>KCH-{yyyy}-{nnnnnn}</c> for ProvisionalCharge.
    /// </summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>Document kind — required on every row.</summary>
    public FeeReceiptKind ReceiptKind { get; set; }

    /// <summary>
    /// Indicates whether this document covers fee-head payments or charge payments.
    /// Redundant with <see cref="ReceiptKind"/> but provides an explicit guard.
    /// </summary>
    public FeeReceiptScope ReceiptScope { get; set; }

    /// <summary>Verification lifecycle for provisional fee and charge receipts.</summary>
    public FeeReceiptVerificationStatus VerificationStatus { get; set; }

    // ── Amounts ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Total amount covered by this receipt (sum of the lines for this scope only).
    /// </summary>
    public double TotalPaidAmount { get; set; }

    [NotMapped]
    public decimal FeePaidAmount => this.GetProperty<decimal>(nameof(FeePaidAmount), ReceiptScope == FeeReceiptScope.Fee ? (decimal)TotalPaidAmount : 0m);

    [NotMapped]
    public decimal ChargePaidAmount => this.GetProperty<decimal>(nameof(ChargePaidAmount), ReceiptScope == FeeReceiptScope.Charge ? (decimal)TotalPaidAmount : 0m);

    public void SetAmountBreakdown(decimal feeAmount, decimal chargeAmount)
    {
        if (feeAmount < 0m || chargeAmount < 0m || feeAmount + chargeAmount != (decimal)TotalPaidAmount)
            throw new BusinessException(ShikshaMitra.Finance.Fees.Collections.FeeCollectionErrorCodes.PaymentDetailsMismatch);
        if (ExtraProperties.ContainsKey(nameof(FeePaidAmount)) &&
            (FeePaidAmount != feeAmount || ChargePaidAmount != chargeAmount))
            throw new BusinessException(ShikshaMitra.Finance.Fees.Collections.FeeCollectionErrorCodes.PaymentDetailsMismatch);
        this.SetProperty(nameof(FeePaidAmount), feeAmount);
        this.SetProperty(nameof(ChargePaidAmount), chargeAmount);
    }

    // ── Student / context ─────────────────────────────────────────────────────

    public Guid? StudentId { get; set; }
    public Guid? FinanceFeeAccountId { get; private set; }

    public void SetFinanceFeeAccount(Guid accountId)
    {
        ShikshaMitra.Finance.Fees.FeeCollection.FinanceFeePayerIdentity.Validate(StudentId, accountId);
        if (FinanceFeeAccountId.HasValue && FinanceFeeAccountId != accountId)
            throw new BusinessException(ShikshaMitra.Finance.Fees.Collections.FinanceFeeAccountErrorCodes.AssignmentConflict);
        FinanceFeeAccountId = accountId;
    }

    public void LinkStudent(Guid studentId) => StudentId =
        ShikshaMitra.Finance.Fees.FeeCollection.FinanceFeePayerIdentity.LinkStudent(StudentId, studentId);
    public Guid AcademicYearId { get; set; }

    /// <summary>Denormalized from student at creation for fast list filtering.</summary>
    public Guid BoardId { get; set; }

    /// <summary>Denormalized from student at creation for fast list filtering.</summary>
    public Guid EducationMediumId { get; set; }

    // ── Receipt date ─────────────────────────────────────────────────────────

    /// <summary>
    /// The date on which this receipt was generated.
    /// Used for the Month filter on the list API.
    /// Defaults to <c>DateOnly.FromDateTime(CreationTime)</c> during backfill.
    /// </summary>
    public DateOnly ReceiptDate { get; set; }

    // ── Blob / PDF ───────────────────────────────────────────────────────────

    /// <summary>
    /// ABP Blob path for the generated PDF (e.g. <c>fee-receipts/{tenantId}/FinalFee/{id}.pdf</c>).
    /// Null until the PDF is first requested or generated at collect time.
    /// </summary>
    public string? PdfBlobName { get; set; }

    // ── Kachi ↔ Final linkage (fee receipts only) ────────────────────────────

    /// <summary>
    /// Set on a <see cref="FeeReceiptKind.FinalFee"/> receipt that was created
    /// by reconciliation. Points to the kachi (<see cref="FeeReceiptKind.ProvisionalFee"/>)
    /// that it replaces.
    /// </summary>
    public Guid? LinkedProvisionalReceiptId { get; set; }

    /// <summary>
    /// Set on a <see cref="FeeReceiptKind.ProvisionalFee"/> receipt once its
    /// final counterpart has been issued. Points to the new FinalFee receipt.
    /// </summary>
    public Guid? LinkedFinalReceiptId { get; set; }

    // ── Future: ReceiptBook numbering ─────────────────────────────────────────

    /// <summary>Optional link to ReceiptBook for prefix/range numbering.</summary>
    public Guid? ReceiptBookId { get; set; }

    public string? Remarks { get; set; }

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Records the blob name after the PDF has been saved to ABP Blob Storing.
    /// </summary>
    public void SetPdfBlobName(string blobName)
    {
        Check.NotNullOrWhiteSpace(blobName, nameof(blobName));
        PdfBlobName = blobName;
    }

    /// <summary>
    /// Sets the back-reference to the provisional receipt on a final receipt.
    /// Called by <c>ReconciliationManager</c> immediately after creating the FIN-* receipt.
    /// </summary>
    public void SetLinkedProvisionalReceiptId(Guid provisionalReceiptId)
    {
        LinkedProvisionalReceiptId = provisionalReceiptId;
    }

    /// <summary>
    /// Marks this provisional fee receipt as verified and links to the newly
    /// created final receipt. Called by the reconciliation module.
    /// </summary>
    public void MarkVerified(Guid finalReceiptId, string? remarks = null)
    {
        if (ReceiptKind is not (FeeReceiptKind.ProvisionalFee or FeeReceiptKind.ProvisionalCharge))
            throw new AbpException("Only provisional receipts can be marked verified.");

        VerificationStatus = FeeReceiptVerificationStatus.Verified;
        LinkedFinalReceiptId = finalReceiptId;
        if (!string.IsNullOrWhiteSpace(remarks))
            Remarks = remarks;
    }

    /// <summary>
    /// Marks this provisional fee receipt as rejected (instrument failed).
    /// </summary>
    public void MarkRejected(string? remarks = null)
    {
        if (ReceiptKind is not (FeeReceiptKind.ProvisionalFee or FeeReceiptKind.ProvisionalCharge))
            throw new AbpException("Only provisional receipts can be marked rejected.");

        VerificationStatus = FeeReceiptVerificationStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(remarks))
            Remarks = remarks;
    }
}
