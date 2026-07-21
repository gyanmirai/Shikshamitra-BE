using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Domain manager responsible for creating fee collection transaction aggregates.
/// All monetary invariants (payment details validation, DueFee allocation) live here
/// so that the application service remains a thin coordinator.
/// </summary>
public class FeeCollectionManager(
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IRepository<FeeCollectionRecord, Guid> feeCollectionRecordRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
	IRepository<FeeCollectionDueFeeAllocation, Guid> dueFeeAllocationRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IFeePaymentReceiptRepository feePaymentReceiptRepository,
	FeeReceiptManager feeReceiptManager,
	IReceiptNumberSequenceRepository receiptNumberSequenceRepository,
	IClock clock,
	ICurrentTenant currentTenant,
	ICurrentUser currentUser
) : DomainService
{
	public sealed record CollectFeeTransactionCommand(
		Guid StudentId,
		Guid AcademicYearId,
		Guid BoardId,
		Guid EducationMediumId,
		FeeCollectionStatus Status,
		double ReceivedAmount,
		string? Notes,
		bool? BankReceiptConfirmed,
		IReadOnlyList<(Guid FeeHeadId, double Amount)> FeeHeadGroups,
		IReadOnlyList<(Guid ChargeId, double Amount)> ChargePayments,
		IReadOnlyList<(Guid DueFeeId, double Amount)> DueFeeAllocations,
		IReadOnlyList<(PaymentMethodEnum Method, double Amount, PaymentDetails? Details)> Payments);

	public sealed record CollectFeeTransactionResult(
		FeeRecordMetadata Metadata,
		string ReceiptNumber,
		List<FeeCollectionRecord> FeeCollectionRecords,
		List<ChargeCollectionRecord> ChargeCollectionRecords,
		List<FeePaymentReceipt> Receipts,
		double FeeLinesTotal,
		double ChargeLinesTotal);

	public async Task<CollectFeeTransactionResult> ExecuteCollectionTransactionAsync(CollectFeeTransactionCommand command)
	{
		var receiptNumber = await GenerateReceiptNumberAsync();

		var metadata = CreateMetadata(
			command.StudentId,
			command.AcademicYearId,
			command.ReceivedAmount,
			command.Status,
			receiptNumber,
			command.Notes);
		await feeRecordMetadataRepository.InsertAsync(metadata, autoSave: false);

		var feeCollectionRecords = CreateFeeCollectionRecords(
			command.StudentId,
			metadata.Id,
			command.FeeHeadGroups);
		await feeCollectionRecordRepository.InsertManyAsync(feeCollectionRecords, autoSave: false);

		var chargeCollectionRecords = new List<ChargeCollectionRecord>();
		if (command.ChargePayments.Count > 0)
		{
			chargeCollectionRecords = CreateChargeCollectionRecords(
				command.StudentId,
				metadata.Id,
				command.ChargePayments);
			await chargeCollectionRecordRepository.InsertManyAsync(chargeCollectionRecords, autoSave: false);
		}

		var paymentRecords = CreatePaymentRecords(
			command.StudentId,
			metadata.Id,
			command.Payments,
			command.Notes);
		await paymentMethodRecordRepository.InsertManyAsync(paymentRecords, autoSave: false);

		if (command.DueFeeAllocations.Count > 0)
		{
			var dueFees = await ApplyDueFeeAllocationsAsync(command.DueFeeAllocations);
			await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: false);

			var feeHeadIds = dueFees.Select(d => d.FeeHeadId).Distinct().ToList();
			var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
			var feeHeadNames = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);
			var dueFeeMap = dueFees.ToDictionary(d => d.Id);

			var allocationRows = command.DueFeeAllocations.Select(a =>
			{
				var row = new FeeCollectionDueFeeAllocation(
					GuidGenerator.Create(),
					currentTenant.Id,
					metadata.Id,
					a.DueFeeId,
					a.Amount);

				if (dueFeeMap.TryGetValue(a.DueFeeId, out var dueFee))
				{
					row.SetScheduleSnapshot(
						feeHeadNames.GetValueOrDefault(dueFee.FeeHeadId),
						dueFee.GetDisplayInstallmentLabel(),
						dueFee.CoverageStartDate,
						dueFee.CoverageEndDate,
						dueFee.ScheduleDueDate,
						dueFee.ScheduleSource);
				}

				return row;
			}).ToList();
			await dueFeeAllocationRepository.InsertManyAsync(allocationRows, autoSave: false);
		}

		var feeLinesTotal = feeCollectionRecords.Sum(r => r.Amount);
		var chargeLinesTotal = chargeCollectionRecords.Sum(r => r.Amount);

		var receipts = await feeReceiptManager.CreateReceiptsForTransactionAsync(
			feeCollectionTransactionId: metadata.Id,
			studentId: command.StudentId,
			academicYearId: command.AcademicYearId,
			boardId: command.BoardId,
			educationMediumId: command.EducationMediumId,
			feeLinesTotal: feeLinesTotal,
			chargeLinesTotal: chargeLinesTotal,
			paymentMethods: command.Payments.Select(p => p.Method).ToList(),
			bankReceiptConfirmed: command.BankReceiptConfirmed,
			remarks: command.Notes);

		if (receipts.Count > 0)
			await feePaymentReceiptRepository.InsertManyAsync(receipts, autoSave: false);

		return new CollectFeeTransactionResult(
			metadata,
			receiptNumber,
			feeCollectionRecords,
			chargeCollectionRecords,
			receipts,
			feeLinesTotal,
			chargeLinesTotal);
	}

	/// <summary>
	/// Generates a unique, concurrent-safe receipt number in the format <c>REC-{YYYY}-{NNNNNN}</c>
	/// using the <see cref="ReceiptNumberSequence"/> table (kind = null slot).
	/// </summary>
	public async Task<string> GenerateReceiptNumberAsync()
	{
		var year = clock.Now.Year;
		var sequence = await receiptNumberSequenceRepository.IncrementAndGetAsync(year, kind: null);
		return $"REC-{year}-{sequence:D6}";
	}

	/// <summary>
	/// Creates the <see cref="FeeRecordMetadata"/> root record for a collection transaction.
	/// </summary>
	public FeeRecordMetadata CreateMetadata(
		Guid studentId,
		Guid academicYearId,
		double received,
		FeeCollectionStatus status,
		string receiptNumber,
		string? remarks)
	{
		return new FeeRecordMetadata(
			GuidGenerator.Create(),
			currentTenant.Id,
			studentId,
			academicYearId,
			received,
			receiptNumber,
			status,
			remarks);
	}

	/// <summary>
	/// Creates one <see cref="FeeCollectionRecord"/> per selected fee head.
	/// </summary>
	public List<FeeCollectionRecord> CreateFeeCollectionRecords(
		Guid studentId,
		Guid metadataId,
		IEnumerable<(Guid FeeHeadId, double Amount)> feeHeads)
	{
		var now = clock.Now;
		return feeHeads.Select(fh => new FeeCollectionRecord(GuidGenerator.Create())
		{
			TenantId = currentTenant.Id,
			StudentId = studentId,
			FeeHeadId = fh.FeeHeadId,
			Amount = fh.Amount,
			PaymentDate = now,
			FeeRecordMetadataId = metadataId,
		}).ToList();
	}

	/// <summary>
	/// Creates one <see cref="ChargeCollectionRecord"/> per selected charge.
	/// </summary>
	public List<ChargeCollectionRecord> CreateChargeCollectionRecords(
		Guid studentId,
		Guid metadataId,
		IEnumerable<(Guid ChargeId, double Amount)> charges)
	{
		var now = clock.Now;
		return charges.Select(c => new ChargeCollectionRecord(GuidGenerator.Create())
		{
			TenantId = currentTenant.Id,
			StudentId = studentId,
			ChargeId = c.ChargeId,
			Amount = c.Amount,
			PaymentDate = now,
			FeeRecordMetadataId = metadataId,
		}).ToList();
	}

	/// <summary>
	/// Creates one <see cref="PaymentMethodRecord"/> per payment instrument in the input.
	/// </summary>
	public List<PaymentMethodRecord> CreatePaymentRecords(
		Guid studentId,
		Guid metadataId,
		IEnumerable<(PaymentMethodEnum Method, double Amount, PaymentDetails? Details)> payments,
		string? notes)
	{
		var now = clock.Now;
		return payments.Select(p => PaymentMethodRecord.Create(
			studentId: studentId,
			feeRecordMetadataId: metadataId,
			amount: (decimal)p.Amount,
			paymentDate: now,
			paymentMethod: p.Method,
			paymentDetails: p.Details,
			tenantId: currentTenant.Id,
			notes: notes)).ToList();
	}

	/// <summary>
	/// Loads <see cref="DueFee"/> rows matching the given ids and calls
	/// <see cref="DueFee.ApplyPayment"/> on each according to the provided allocations.
	/// Returns the mutated entities — the caller persists them.
	/// </summary>
	public async Task<List<DueFee>> ApplyDueFeeAllocationsAsync(
		IReadOnlyList<(Guid DueFeeId, double Amount)> allocations)
	{
		var ids = allocations.Select(a => a.DueFeeId).Distinct().ToList();
		var dueFees = await dueFeeRepository.GetListAsync(df => ids.Contains(df.Id));

		if (dueFees.Count != ids.Count)
			throw new BusinessException(DueFeeErrorCodes.DueFeeNotFound);

		var map = dueFees.ToDictionary(df => df.Id);
		foreach (var (dueFeeId, amount) in allocations)
			map[dueFeeId].ApplyPayment(amount);

		return dueFees;
	}

	/// <summary>
	/// Reverses DueFee payment allocations that were persisted during fee collection,
	/// restoring outstanding balances to their pre-payment state.
	/// Called by the reconciliation module when an instrument is rejected.
	/// </summary>
	public async Task<List<DueFee>> ReverseDueFeeAllocationsAsync(
		IReadOnlyList<(Guid DueFeeId, double Amount)> allocations,
		CancellationToken cancellationToken = default)
	{
		var ids = allocations.Select(a => a.DueFeeId).Distinct().ToList();
		var dueFees = await dueFeeRepository.GetListAsync(
			df => ids.Contains(df.Id), cancellationToken: cancellationToken);

		if (dueFees.Count != ids.Count)
			throw new BusinessException(DueFeeErrorCodes.DueFeeNotFound);

		var map = dueFees.ToDictionary(df => df.Id);
		foreach (var (dueFeeId, amount) in allocations)
			map[dueFeeId].ReversePayment(amount);

		return dueFees;
	}

	/// <summary>
	/// Maps the raw details dictionary from the API request into the appropriate
	/// strongly-typed <see cref="PaymentDetails"/> value object.
	/// Returns <c>null</c> for Cash — no instrument details required.
	/// </summary>
	public static PaymentDetails? MapPaymentDetails(
		PaymentMethodEnum method,
		Dictionary<string, object?>? details)
	{
		if (method == PaymentMethodEnum.Cash)
			return null;

		details ??= [];

		string Get(string key) =>
			details.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

		return method switch
		{
			PaymentMethodEnum.Cheque => new ChequeDetails(
				chequeNumber: Get("chequeNumber"),
				bankName: Get("bankName"),
				branchName: Get("branchName"),
				chequeDate: DateOnly.TryParse(Get("chequeDate"), out var cd)
					? cd : DateOnly.FromDateTime(DateTime.UtcNow)),

			PaymentMethodEnum.DemandDraft => new DemandDraftDetails(
				ddNumber: Get("ddNumber"),
				issuingBankName: Get("issuingBankName"),
				branchName: Get("branchName"),
				ddDate: DateOnly.TryParse(Get("ddDate"), out var dd)
					? dd : DateOnly.FromDateTime(DateTime.UtcNow)),

			PaymentMethodEnum.BankTransfer => new BankTransferDetails(
				transactionReferenceNumber: Get("transactionReferenceNumber"),
				bankName: Get("bankName"),
				ifscCode: Get("ifscCode")),

			PaymentMethodEnum.UPI => new UpiDetails(
				upiTransactionId: Get("upiTransactionId"),
				upiId: Get("upiId"),
				appName: Get("appName")),

			PaymentMethodEnum.CreditCard => new CreditCardDetails(
				transactionId: Get("transactionId"),
				cardLast4: Get("cardLast4"),
				bankName: Get("bankName")),

			PaymentMethodEnum.DebitCard => new CreditCardDetails(
				transactionId: Get("transactionId"),
				cardLast4: Get("cardLast4"),
				bankName: Get("bankName")),

			_ => null,
		};
	}
}
