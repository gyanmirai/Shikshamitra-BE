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
public class ReconciliationManager : DomainService
{
	private readonly IFeePaymentReceiptRepository receiptRepository;
	private readonly IRepository<FeeRecordMetadata, Guid> metadataRepository;
	private readonly IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository;
	private readonly IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository;
	private readonly IDueFeeRepository dueFeeRepository;
	private readonly IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository;
	private readonly IRepository<Charge, Guid> chargeRepository;
	private readonly IRepository<AcademicYear, Guid> academicYearRepository;
	private readonly FeeReceiptManager feeReceiptManager;
	private readonly FeeCollectionManager feeCollectionManager;
	private readonly IFeeReceiptPdfGenerator pdfGenerator;
	private readonly FeeReceiptBlobManager blobManager;
	private readonly ShikshaMitra.Finance.Accounts.AutomaticTransactionManager automaticTransactions;

	public ReconciliationManager(
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
	)
	{
		this.receiptRepository = receiptRepository;
		this.metadataRepository = metadataRepository;
		this.paymentMethodRecordRepository = paymentMethodRecordRepository;
		this.allocationRepository = allocationRepository;
		this.dueFeeRepository = dueFeeRepository;
		this.chargeCollectionRecordRepository = chargeCollectionRecordRepository;
		this.chargeRepository = chargeRepository;
		this.academicYearRepository = academicYearRepository;
		this.feeReceiptManager = feeReceiptManager;
		this.feeCollectionManager = feeCollectionManager;
		this.pdfGenerator = pdfGenerator;
		this.blobManager = blobManager;
		this.automaticTransactions = automaticTransactions;
	}

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
		var originals = await GetVerifiableReceiptsAsync(provisional, cancellationToken);
        FeePaymentReceipt? selectedFinal = null;
        foreach (var original in originals)
        {
            var finalKind = original.ReceiptKind == FeeReceiptKind.ProvisionalFee ? FeeReceiptKind.FinalFee : FeeReceiptKind.FinalCharge;
            var finalReceiptNumber = await feeReceiptManager.GenerateReceiptNumberAsync(finalKind);
            var finalReceipt = feeReceiptManager.CreateReceipt(
                original.FeeCollectionTransactionId, original.StudentId, original.AcademicYearId,
                original.BoardId, original.EducationMediumId, original.ReceiptScope, finalKind,
                original.TotalPaidAmount, finalReceiptNumber, remarks, original.ReceiptBookId, original.FinanceFeeAccountId);
            if (original.ReceiptScope == FeeReceiptScope.Combined)
                finalReceipt.SetAmountBreakdown(original.FeePaidAmount, original.ChargePaidAmount);
            if (original.LateFeePaidAmount.HasValue)
                finalReceipt.SetLateFeePaidAmount(original.LateFeePaidAmount.Value);
            finalReceipt.SetLinkedProvisionalReceiptId(original.Id);
            await receiptRepository.InsertAsync(finalReceipt, autoSave: false);
            var student = original.StudentId.HasValue
                ? await LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Linq.IAsyncQueryableExecuter>().FirstOrDefaultAsync((await LazyServiceProvider.LazyGetRequiredService<IStudentRepository>().GetQueryableWithoutAutoIncludesAsync()).Where(s => s.Id == original.StudentId.Value), cancellationToken) : null;
            var academicYear = original.AcademicYearId != Guid.Empty
                ? await academicYearRepository.FindAsync(original.AcademicYearId, includeDetails: false, cancellationToken: cancellationToken) : null;
            await GenerateAndStoreReceiptPdfsAsync(new[] { finalReceipt }, student, academicYear, cancellationToken);
            original.MarkVerified(finalReceipt.Id, remarks);
            await receiptRepository.UpdateAsync(original, autoSave: false, cancellationToken: cancellationToken);
            if (original.Id == provisionalReceiptId) selectedFinal = finalReceipt;
        }

		var metadata = await metadataRepository.FindAsync(provisional.FeeCollectionTransactionId, cancellationToken: cancellationToken);
		if (metadata != null)
		{
			var calculatedStatus = await DeterminePostApprovalStatusAsync(metadata.Id, cancellationToken);
			if (calculatedStatus == FeeCollectionStatus.Paid) metadata.MarkPaid();
			else metadata.MarkPartial();
			await metadataRepository.UpdateAsync(metadata, autoSave: false);
		}

		await automaticTransactions.ConfirmAsync(provisional.FeeCollectionTransactionId);
		return selectedFinal!;
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
		var originals = await GetVerifiableReceiptsAsync(provisional, cancellationToken);
        foreach (var original in originals)
        {
            original.MarkRejected(remarks);
            await receiptRepository.UpdateAsync(original, autoSave: false, cancellationToken: cancellationToken);
        }

		if (originals.Any(r => r.ReceiptScope is FeeReceiptScope.Fee or FeeReceiptScope.Combined))
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
		if (originals.Any(r => r.ReceiptScope is FeeReceiptScope.Charge or FeeReceiptScope.Combined))
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
	/// receipt. With one instrument, all original documents are verified together.
	/// Multiple instruments remain blocked because their allocation shares are ambiguous.
	/// </summary>
	private async Task<List<FeePaymentReceipt>> GetVerifiableReceiptsAsync(
        FeePaymentReceipt provisional, CancellationToken cancellationToken)
    {
        var payments = await paymentMethodRecordRepository.GetListAsync(
            p => p.FeeRecordMetadataId == provisional.FeeCollectionTransactionId, cancellationToken: cancellationToken);
        var originals = await receiptRepository.GetListAsync(
            r => r.FeeCollectionTransactionId == provisional.FeeCollectionTransactionId && r.LinkedProvisionalReceiptId == null,
            cancellationToken: cancellationToken);
        // One instrument is settled as one atomic decision. Separate documents remain
        // two queue rows, each linked to its own final document. Never guess mixed-instrument shares.
        if (payments.Count != 1 || originals.Count is < 1 or > 2 ||
            originals.Select(r => r.ReceiptScope).Distinct().Count() != originals.Count ||
            originals.Any(r => r.StudentId != provisional.StudentId || r.FinanceFeeAccountId != provisional.FinanceFeeAccountId ||
                r.AcademicYearId != provisional.AcademicYearId ||
                r.VerificationStatus != FeeReceiptVerificationStatus.PendingVerification ||
                r.ReceiptKind is not (FeeReceiptKind.ProvisionalFee or FeeReceiptKind.ProvisionalCharge)) ||
            originals.Sum(r => (decimal)r.TotalPaidAmount) != payments[0].Amount ||
            originals.Count == 2 && (originals.All(r => r.ReceiptScope != FeeReceiptScope.Fee) ||
                originals.All(r => r.ReceiptScope != FeeReceiptScope.Charge)))
            throw new BusinessException(ReconciliationErrorCodes.InstrumentAttributionRequired)
                .WithData("FeeCollectionTransactionId", provisional.FeeCollectionTransactionId);
        return originals;
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
		Student? student,
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
