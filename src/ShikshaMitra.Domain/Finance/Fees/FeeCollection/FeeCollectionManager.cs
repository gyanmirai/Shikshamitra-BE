using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.FeeCollection;
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
	IDueFeeRepository dueFeeRepository,
	IClock clock,
	ICurrentTenant currentTenant,
	ICurrentUser currentUser
) : DomainService
{
	/// <summary>
	/// Generates a unique receipt number in the format <c>REC-{YYYY}-{NNNNNN}</c>.
	/// The sequence is derived from the total row count of existing receipts plus one.
	/// </summary>
	public async Task<string> GenerateReceiptNumberAsync()
	{
		var year = clock.Now.Year;
		var count = await feeRecordMetadataRepository.CountAsync();
		var sequence = (count + 1).ToString("D6");
		return $"REC-{year}-{sequence}";
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
		return new FeeRecordMetadata(GuidGenerator.Create())
		{
			TenantId = currentTenant.Id,
			StudentId = studentId,
			AcademicYearId = academicYearId,
			TotalPaidFee = received,
			ReceiptNumber = receiptNumber,
			Status = status,
			Remarks = remarks,
		};
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

			PaymentMethodEnum.OnlinePaymentGateway => new OnlineGatewayDetails(
				transactionId: Get("transactionId"),
				gatewayName: Get("gatewayName"),
				orderId: details.TryGetValue("orderId", out var oid) ? oid?.ToString() : null),

			PaymentMethodEnum.WalletPrepaidCard => new WalletDetails(
				transactionId: Get("transactionId"),
				walletProvider: Get("walletProvider"),
				mobileNumber: details.TryGetValue("mobileNumber", out var mob) ? mob?.ToString() : null),

			_ => null,
		};
	}
}
