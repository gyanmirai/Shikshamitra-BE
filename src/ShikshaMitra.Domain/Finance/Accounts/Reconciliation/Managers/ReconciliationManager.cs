using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

/// <summary>
/// Domain manager that orchestrates the reconciliation approve/reject workflow.
/// Keeps aggregates thin — FeePaymentReceipt only exposes MarkVerified / MarkRejected;
/// this manager coordinates the cross-aggregate work inside one unit of work.
/// Mirrors EmployeeLeaveManager in the HR module.
/// </summary>
public class ReconciliationManager(
    IFeePaymentReceiptRepository receiptRepository,
    IRepository<FeeRecordMetadata, Guid> metadataRepository,
    IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository,
    IDueFeeRepository dueFeeRepository,
    IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
    IRepository<Charge, Guid> chargeRepository,
	IRepository<Student, Guid> studentRepository,
	FeeReceiptManager feeReceiptManager,
    FeeCollectionManager feeCollectionManager,
	IFeeReceiptPdfGenerator pdfGenerator,
	FeeReceiptBlobManager blobManager,
	IClock clock,
    ICurrentTenant currentTenant
) : DomainService
{
    /// <summary>
    /// Approves a provisional fee receipt:
    /// 1. Guards: must be ProvisionalFee + PendingVerification
    /// 2. Creates a FinalFee (FIN-*) receipt
    /// 3. Links provisional ↔ final via LinkedFinalReceiptId / LinkedProvisionalReceiptId
    /// 4. Calls MarkVerified on the provisional receipt
    /// </summary>
    public async Task<FeePaymentReceipt> ApproveAsync(
        Guid provisionalReceiptId,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        var provisional = await receiptRepository.GetAsync(provisionalReceiptId, cancellationToken: cancellationToken)
            ?? throw new BusinessException(ReconciliationErrorCodes.ReceiptNotFound)
                .WithData("ReceiptId", provisionalReceiptId);

        EnsureProvisionalFee(provisional);
        EnsureNotTerminal(provisional);

        // Create FIN-* final receipt linked to this provisional
        var finalReceiptNumber = await feeReceiptManager.GenerateReceiptNumberAsync(FeeReceiptKind.FinalFee);
        var finalReceipt = feeReceiptManager.CreateReceipt(
            feeCollectionTransactionId : provisional.FeeCollectionTransactionId,
            studentId                  : provisional.StudentId,
            academicYearId             : provisional.AcademicYearId,
            boardId                    : provisional.BoardId,
            educationMediumId          : provisional.EducationMediumId,
            scope                      : FeeReceiptScope.Fee,
            kind                       : FeeReceiptKind.FinalFee,
            totalPaidAmount            : provisional.TotalPaidAmount,
            receiptNumber              : finalReceiptNumber,
            remarks                    : remarks,
            receiptBookId              : provisional.ReceiptBookId);

        // Set cross-link — provisional points to final, final points to provisional
        finalReceipt.SetLinkedProvisionalReceiptId(provisional.Id);
        await receiptRepository.InsertAsync(finalReceipt, autoSave: false);

		var student = await studentRepository.GetAsync(provisional.StudentId, cancellationToken: cancellationToken)
			?? throw new BusinessException("Student not found")
				.WithData("StudentId", provisional.StudentId);

		await GenerateAndStoreReceiptPdfsAsync(new[] { finalReceipt }, student, student?.AcademicYear, cancellationToken);

		provisional.MarkVerified(finalReceipt.Id, remarks);

        // Update the parent FeeRecordMetadata Status to Paid or Partial
        var metadata = await metadataRepository.FindAsync(provisional.FeeCollectionTransactionId, cancellationToken: cancellationToken);
        if (metadata != null)
        {
            var calculatedStatus = FeeCollectionStatus.Paid;

            // Check if any allocated DueFee remains unpaid (OutstandingAmount > 0)
            var allocations = await allocationRepository.GetListAsync(
                a => a.FeeRecordMetadataId == metadata.Id,
                cancellationToken: cancellationToken);
            if (allocations.Count > 0)
            {
                var dueFeeIds = allocations.Select(a => a.DueFeeId).Distinct().ToList();
                var dueFees = await dueFeeRepository.GetListAsync(
                    df => dueFeeIds.Contains(df.Id),
                    cancellationToken: cancellationToken);
                if (dueFees.Any(df => df.OutstandingAmount > 0))
                {
                    calculatedStatus = FeeCollectionStatus.Partial;
                }
            }

            // Check if any associated Charge remains unpaid
            var chargeRecords = await chargeCollectionRecordRepository.GetListAsync(
                r => r.FeeRecordMetadataId == metadata.Id,
                cancellationToken: cancellationToken);
            if (chargeRecords.Count > 0 && calculatedStatus == FeeCollectionStatus.Paid)
            {
                var chargeIds = chargeRecords.Select(r => r.ChargeId).Distinct().ToList();
                var charges = await chargeRepository.GetListAsync(
                    c => chargeIds.Contains(c.Id),
                    cancellationToken: cancellationToken);

                var allPaymentsForCharges = await chargeCollectionRecordRepository.GetListAsync(
                    r => chargeIds.Contains(r.ChargeId),
                    cancellationToken: cancellationToken);
                var paidPerCharge = allPaymentsForCharges
                    .GroupBy(r => r.ChargeId)
                    .ToDictionary(g => g.Key, g => (decimal)g.Sum(r => r.Amount));

                if (charges.Any(c => c.Amount - paidPerCharge.GetValueOrDefault(c.Id, 0m) > 0m))
                {
                    calculatedStatus = FeeCollectionStatus.Partial;
                }
            }

            metadata.Status = calculatedStatus;
            await metadataRepository.UpdateAsync(metadata, autoSave: false);
        }

        return finalReceipt;
    }

    /// <summary>
    /// Rejects a provisional fee receipt with full financial rollback:
    /// 1. Guards: must be ProvisionalFee + PendingVerification
    /// 2. Marks receipt as Rejected
    /// 3. Loads persisted DueFee allocations and reverses them
    /// 4. Marks the parent FeeRecordMetadata as Reversed
    /// </summary>
    public async Task<FeePaymentReceipt> RejectAsync(
        Guid provisionalReceiptId,
        string remarks,
        CancellationToken cancellationToken = default)
    {
        var provisional = await receiptRepository.GetAsync(provisionalReceiptId, cancellationToken: cancellationToken)
            ?? throw new BusinessException(ReconciliationErrorCodes.ReceiptNotFound)
                .WithData("ReceiptId", provisionalReceiptId);

        EnsureProvisionalFee(provisional);
        EnsureNotTerminal(provisional);

        provisional.MarkRejected(remarks);

        // Reverse DueFee allocations
        var allocations = await allocationRepository
            .GetListAsync(a => a.FeeRecordMetadataId == provisional.FeeCollectionTransactionId,
                cancellationToken: cancellationToken);

        if (allocations.Count > 0)
        {
            var allocationPairs = allocations
                .Select(a => (a.DueFeeId, a.Amount))
                .ToList();
            var dueFees = await feeCollectionManager.ReverseDueFeeAllocationsAsync(allocationPairs, cancellationToken);
            await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: false);
        }

        // Mark the collection transaction as Reversed
        var metadata = await metadataRepository.FindAsync(
            provisional.FeeCollectionTransactionId, cancellationToken: cancellationToken)
            ?? throw new BusinessException(ReconciliationErrorCodes.ReceiptNotFound)
                .WithData("FeeCollectionTransactionId", provisional.FeeCollectionTransactionId);

        metadata.MarkReversed(remarks);

        return provisional;
    }

    // ── Guard helpers ─────────────────────────────────────────────────────────

    private static void EnsureProvisionalFee(FeePaymentReceipt receipt)
    {
        if (receipt.ReceiptKind != FeeReceiptKind.ProvisionalFee)
            throw new BusinessException(ReconciliationErrorCodes.NotProvisionalFeeReceipt)
                .WithData("ReceiptId", receipt.Id)
                .WithData("Kind", receipt.ReceiptKind);
    }

    private static void EnsureNotTerminal(FeePaymentReceipt receipt)
    {
        if (receipt.VerificationStatus == FeeReceiptVerificationStatus.Verified)
            throw new BusinessException(ReconciliationErrorCodes.AlreadyVerified)
                .WithData("ReceiptId", receipt.Id);

        if (receipt.VerificationStatus == FeeReceiptVerificationStatus.Rejected)
            throw new BusinessException(ReconciliationErrorCodes.AlreadyRejected)
                .WithData("ReceiptId", receipt.Id);
    }

	private async Task GenerateAndStoreReceiptPdfsAsync(
		IReadOnlyList<FeePaymentReceipt> receipts,
		Student student,
		AcademicYear academicYear,
		CancellationToken cancellationToken = default)
	{
		foreach (var receipt in receipts)
		{
			var pdfBytes = await pdfGenerator.GenerateAsync(receipt, student, academicYear, cancellationToken);
			var blobName = await blobManager.SavePdfAsync(receipt.Id, receipt.ReceiptKind, pdfBytes, cancellationToken);
			receipt.SetPdfBlobName(blobName);
		}
	}
}
