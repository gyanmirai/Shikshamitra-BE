using System;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Receipts;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// A printable fee or charge receipt document.
/// <para>
/// One receipt is created per <see cref="FeeReceiptScope"/> per collection event.
/// A single <c>CollectFee</c> that includes both fee lines and charge lines
/// produces two separate receipt rows — one with <see cref="FeeReceiptScope.Fee"/>
/// and one with <see cref="FeeReceiptScope.Charge"/>.
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

    /// <summary>Verification lifecycle, meaningful only for <see cref="FeeReceiptKind.ProvisionalFee"/>.</summary>
    public FeeReceiptVerificationStatus VerificationStatus { get; set; }

    // ── Amounts ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Total amount covered by this receipt (sum of the lines for this scope only).
    /// </summary>
    public double TotalPaidAmount { get; set; }

    // ── Student / context ─────────────────────────────────────────────────────

    public Guid StudentId { get; set; }
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
    /// Marks this provisional fee receipt as verified and links to the newly
    /// created final receipt. Called by the reconciliation module.
    /// </summary>
    public void MarkVerified(Guid finalReceiptId)
    {
        if (ReceiptKind != FeeReceiptKind.ProvisionalFee)
            throw new AbpException("Only ProvisionalFee receipts can be marked verified.");

        VerificationStatus = FeeReceiptVerificationStatus.Verified;
        LinkedFinalReceiptId = finalReceiptId;
    }

    /// <summary>
    /// Marks this provisional fee receipt as rejected (instrument failed).
    /// </summary>
    public void MarkRejected()
    {
        if (ReceiptKind != FeeReceiptKind.ProvisionalFee)
            throw new AbpException("Only ProvisionalFee receipts can be marked rejected.");

        VerificationStatus = FeeReceiptVerificationStatus.Rejected;
    }
}
