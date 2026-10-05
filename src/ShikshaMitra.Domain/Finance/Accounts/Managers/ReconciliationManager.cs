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
/// Domain manager that orchestrates the reconciliation approve/reject workflow for both
/// provisional fee receipts (KFE) and provisional charge receipts (KCH).
/// </summary>
public class ReconciliationManager(
	IFeePaymentReceiptRepository receiptRepository,
	IRepository<FeeRecordMetadata, Guid> metadataRepository,
	IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
	IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<Student, Guid> studentRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	FeeReceiptManager feeReceiptManager,
	FeeCollectionManager feeCollectionManager,
	IFeeReceiptPdfGenerator pdfGenerator,
	FeeReceiptBlobManager blobManager,
	ShikshaMitra.Finance.Accounts.AutomaticTransactionManager automaticTransactions,
	IClock clock,
	ICurrentTenant currentTenant
) : DomainService
{
	/// <summary>
	/// Approves a provisional receipt (KFE or KCH):
	/// - KFE → creates FIN-* final fee receipt, updates DueFee allocation statuses
	/// - KCH → creates FCH-* final charge receipt
	/// In both cases updates the parent FeeRecordMetadata to Paid or Partial.
	/// </summary>
	public async Task<FeePaymentReceipt> ApproveAsync(
		Guid provisionalReceiptId,
		string? remarks,
		CancellationToken cancellationToken = default)
	{
		var provisional = await receiptRepository.GetAsync(provisionalReceiptId, cancellationToken: cancellationToken)
			?? throw new BusinessException(ReconciliationErrorCodes.ReceiptNotFound)
				.WithData("ReceiptId", provisionalReceiptId);

		EnsureProvisional(provisional);
		EnsureNotTerminal(provisional);
		await EnsureUnambiguousCollectionAsync(provisional, cancellationToken);

		var finalKind = provisional.ReceiptKind == FeeReceiptKind.ProvisionalFee
			? FeeReceiptKind.FinalFee
			: FeeReceiptKind.FinalCharge;

		var finalReceiptNumber = await feeReceiptManager.GenerateReceiptNumberAsync(finalKind);
		var finalReceipt = feeReceiptManager.CreateReceipt(
			feeCollectionTransactionId: provisional.FeeCollectionTransactionId,
			studentId: provisional.StudentId, academicYearId: provisional.AcademicYearId,
			boardId: provisional.BoardId, educationMediumId: provisional.EducationMediumId,
			scope: provisional.ReceiptScope, kind: finalKind,
			totalPaidAmount: provisional.TotalPaidAmount,
			receiptNumber: finalReceiptNumber, remarks: remarks,
			receiptBookId: provisional.ReceiptBookId);

		finalReceipt.SetLinkedProvisionalReceiptId(provisional.Id);
		await receiptRepository.InsertAsync(finalReceipt, autoSave: false);

		var student = await studentRepository.GetAsync(provisional.StudentId, cancellationToken: cancellationToken)
			?? throw new BusinessException("Student not found").WithData("StudentId", provisional.StudentId);
		AcademicYear? academicYear = provisional.AcademicYearId != Guid.Empty
			? await academicYearRepository.FindAsync(provisional.AcademicYearId, cancellationToken: cancellationToken)
			: null;

		await GenerateAndStoreReceiptPdfsAsync(new[] { finalReceipt }, student, academicYear, cancellationToken);
		provisional.MarkVerified(finalReceipt.Id, remarks);

		var metadata = await metadataRepository.FindAsync(provisional.FeeCollectionTransactionId, cancellationToken: cancellationToken);
		if (metadata != null)
		{
			var calculatedStatus = await DeterminePostApprovalStatusAsync(metadata.Id, cancellationToken);
			if (calculatedStatus == FeeCollectionStatus.Paid) metadata.MarkPaid();
			else metadata.MarkPartial();
			await metadataRepository.UpdateAsync(metadata, autoSave: false);
		}

		await automaticTransactions.ConfirmAsync(provisional.FeeCollectionTransactionId);
		return finalReceipt;
	}

	/// <summary>
	/// Rejects a provisional receipt (KFE or KCH) with full financial rollback:
	/// - KFE → reverses DueFee allocations
	/// - KCH → reverses ChargeCollectionRecord entries
	/// In both cases marks the parent FeeRecordMetadata as Reversed.
	/// </summary>
	public async Task<FeePaymentReceipt> RejectAsync(
		Guid provisionalReceiptId,
		string remarks,
		CancellationToken cancellationToken = default)
	{
		var provisional = await receiptRepository.GetAsync(provisionalReceiptId, cancellationToken: cancellationToken)
			?? throw new BusinessException(ReconciliationErrorCodes.ReceiptNotFound)
				.WithData("ReceiptId", provisionalReceiptId);

		EnsureProvisional(provisional);
		EnsureNotTerminal(provisional);
		await EnsureUnambiguousCollectionAsync(provisional, cancellationToken);

		provisional.MarkRejected(remarks);

		if (provisional.ReceiptKind == FeeReceiptKind.ProvisionalFee)
		{
			var allocations = await allocationRepository.GetListAsync(
				a => a.FeeRecordMetadataId == provisional.FeeCollectionTransactionId, cancellationToken: cancellationToken);
			if (allocations.Count > 0)
			{
				var allocationPairs = allocations.Select(a => (a.DueFeeId, a.Amount)).ToList();
				var dueFees = await feeCollectionManager.ReverseDueFeeAllocationsAsync(allocationPairs, cancellationToken);
				await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: false);
			}
		}
		else
		{
			// Reverse ChargeCollectionRecord entries for charge receipts
			var chargeRecords = await chargeCollectionRecordRepository.GetListAsync(
				r => r.FeeRecordMetadataId == provisional.FeeCollectionTransactionId, cancellationToken: cancellationToken);
			if (chargeRecords.Count > 0)
				await chargeCollectionRecordRepository.DeleteManyAsync(chargeRecords, autoSave: false);
		}

		var metadata = await metadataRepository.FindAsync(
			provisional.FeeCollectionTransactionId, cancellationToken: cancellationToken)
			?? throw new BusinessException(ReconciliationErrorCodes.ReceiptNotFound)
				.WithData("FeeCollectionTransactionId", provisional.FeeCollectionTransactionId);

		await automaticTransactions.ReverseAsync(metadata.Id);
		metadata.MarkReversed(remarks);
		return provisional;
	}

	// ── Private helpers ───────────────────────────────────────────────────────

	/// <summary>
	/// The current model links allocations to a collection, not to an instrument or
	/// receipt. A receipt-level decision is safe only for one instrument and one
	/// original receipt. Otherwise rejection would reverse unrelated money.
	/// </summary>
	private async Task EnsureUnambiguousCollectionAsync(
		FeePaymentReceipt provisional, CancellationToken cancellationToken)
	{
		var paymentCount = await paymentMethodRecordRepository.CountAsync(
			p => p.FeeRecordMetadataId == provisional.FeeCollectionTransactionId,
			cancellationToken);
		var originalReceiptCount = await receiptRepository.CountAsync(
			r => r.FeeCollectionTransactionId == provisional.FeeCollectionTransactionId
				&& r.LinkedProvisionalReceiptId == null,
			cancellationToken);
		if (paymentCount != 1 || originalReceiptCount != 1)
			throw new BusinessException(ReconciliationErrorCodes.InstrumentAttributionRequired)
				.WithData("FeeCollectionTransactionId", provisional.FeeCollectionTransactionId);
	}

	private async Task<FeeCollectionStatus> DeterminePostApprovalStatusAsync(
		Guid metadataId, CancellationToken cancellationToken)
	{
		var allocations = await allocationRepository.GetListAsync(
			a => a.FeeRecordMetadataId == metadataId, cancellationToken: cancellationToken);

		if (allocations.Count > 0)
		{
			var dueFeeIds = allocations.Select(a => a.DueFeeId).Distinct().ToList();
			var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id), cancellationToken: cancellationToken);
			if (dueFees.Any(df => df.OutstandingAmount > 0))
				return FeeCollectionStatus.Partial;
		}

		var chargeRecords = await chargeCollectionRecordRepository.GetListAsync(
			r => r.FeeRecordMetadataId == metadataId, cancellationToken: cancellationToken);
		if (chargeRecords.Count > 0)
		{
			var chargeIds = chargeRecords.Select(r => r.ChargeId).Distinct().ToList();
			var charges = await chargeRepository.GetListAsync(c => chargeIds.Contains(c.Id), cancellationToken: cancellationToken);
			var allPayments = await chargeCollectionRecordRepository.GetListAsync(r => chargeIds.Contains(r.ChargeId), cancellationToken: cancellationToken);
			var paidPerCharge = allPayments.GroupBy(r => r.ChargeId)
				.ToDictionary(g => g.Key, g => (decimal)g.Sum(r => r.Amount));
			if (charges.Any(c => c.Amount - paidPerCharge.GetValueOrDefault(c.Id, 0m) > 0m))
				return FeeCollectionStatus.Partial;
		}

		return FeeCollectionStatus.Paid;
	}

	private static void EnsureProvisional(FeePaymentReceipt receipt)
	{
		if (receipt.ReceiptKind != FeeReceiptKind.ProvisionalFee && receipt.ReceiptKind != FeeReceiptKind.ProvisionalCharge)
			throw new BusinessException(ReconciliationErrorCodes.NotProvisionalFeeReceipt)
				.WithData("ReceiptId", receipt.Id).WithData("Kind", receipt.ReceiptKind);
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
		AcademicYear? academicYear,
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
