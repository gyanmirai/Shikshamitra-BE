using System;
using System.Text.Json;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Finance.Fees.Receipts;

public sealed record FinanceReceiptPayerEvidence(FinanceExternalPayerKind Kind, Guid PersonId, string Name, string? Code);

public sealed record FinanceReceiptSettlementEvidence(decimal OriginalAmount, decimal SubmittedAmount,
	decimal SettledAmount, decimal PendingVerificationAmount, decimal FinancialOutstandingAmount,
	decimal CollectibleAmount, decimal NetWaivedAmount = 0m);

public static class FinanceReceiptEvidence
{
	private const string PayerProperty = "ReceiptPayerSnapshotV1";
	private const string SettlementProperty = "ReceiptSettlementSnapshotV1";
	public static void CapturePayer(FeeRecordMetadata metadata, FinanceReceiptPayerEvidence payer)
	{
		EnsurePayer(metadata, payer);
		Capture(metadata, PayerProperty, payer);
	}
	public static FinanceReceiptPayerEvidence? ReadPayer(FeeRecordMetadata? metadata)
	{
		var payer = Read<FinanceReceiptPayerEvidence>(metadata, PayerProperty);
		if (payer != null) EnsurePayer(metadata!, payer);
		return payer;
	}
	public static FinanceReceiptSettlementEvidence FromBalance(FinanceExternalBalanceAmounts balance, decimal submitted) =>
		new(balance.Original, submitted, balance.Settled, balance.PendingVerification,
			balance.Unsettled, balance.Collectible, balance.NetWaived);
	public static void CaptureSettlement(IHasExtraProperties target, FinanceReceiptSettlementEvidence evidence)
	{
		EnsureSettlement(evidence);
		Capture(target, SettlementProperty, evidence);
	}
	public static FinanceReceiptSettlementEvidence? ReadSettlement(FeePaymentReceipt receipt, FeeRecordMetadata? metadata)
	{
		var result = Read<FinanceReceiptSettlementEvidence>(receipt, SettlementProperty)
			?? Read<FinanceReceiptSettlementEvidence>(metadata, SettlementProperty);
		if (result != null)
		{
			EnsureSettlement(result);
			if (result.SubmittedAmount != receipt.TotalPaidAmount) throw Invalid();
		}
		return result;
	}
	private static void EnsurePayer(FeeRecordMetadata metadata, FinanceReceiptPayerEvidence payer)
	{
		if (payer.PersonId == Guid.Empty || string.IsNullOrWhiteSpace(payer.Name) || payer.Name.Length > 500 || payer.Code?.Length > 100 ||
			payer.Kind == FinanceExternalPayerKind.Student && (metadata.StudentId != payer.PersonId || metadata.EmployeeId.HasValue) ||
			payer.Kind == FinanceExternalPayerKind.Employee && (metadata.EmployeeId != payer.PersonId || metadata.StudentId.HasValue) ||
			!Enum.IsDefined(payer.Kind) || metadata.FinanceFeeAccountId.HasValue) throw Invalid();
	}
	private static void EnsureSettlement(FinanceReceiptSettlementEvidence evidence)
	{
		foreach (var amount in new[] { evidence.OriginalAmount, evidence.SubmittedAmount, evidence.SettledAmount,
			evidence.PendingVerificationAmount, evidence.FinancialOutstandingAmount, evidence.CollectibleAmount, evidence.NetWaivedAmount })
			FinanceExternalSettlementAmounts.EnsureMoney(amount);
		if (evidence.OriginalAmount <= 0m || evidence.SubmittedAmount <= 0m ||
			evidence.SubmittedAmount > evidence.OriginalAmount ||
			evidence.FinancialOutstandingAmount != evidence.OriginalAmount - evidence.SettledAmount - evidence.NetWaivedAmount ||
			evidence.CollectibleAmount != evidence.FinancialOutstandingAmount - evidence.PendingVerificationAmount)
			throw Invalid();
	}
	private static void Capture<T>(IHasExtraProperties target, string property, T value) where T : class
	{
		var prior = Read<T>(target, property);
		if (prior != null && !Equals(prior, value)) throw Invalid();
		if (prior == null) target.SetProperty(property, JsonSerializer.Serialize(value));
	}
	private static T? Read<T>(IHasExtraProperties? target, string property) where T : class
	{
		var json = target?.GetProperty<string>(property);
		if (string.IsNullOrWhiteSpace(json)) return default;
		try { return JsonSerializer.Deserialize<T>(json) ?? throw Invalid(); }
		catch (JsonException) { throw Invalid(); }
	}
	private static BusinessException Invalid() => new(LibraryFinanceErrorCodes.SourceRequiresReview,
		"The saved receipt evidence requires Finance review.");
}
