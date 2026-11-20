using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Library;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public partial class FeeCollectionManager
{
	public sealed record ExternalPayment(PaymentMethodEnum Method, decimal Amount, PaymentDetails? Details,
		Guid? BankAccountId, string? BankLabel);
	public sealed record ExternalCollection(FeeRecordMetadata Metadata, ChargeCollectionRecord Allocation,
		IReadOnlyList<PaymentMethodRecord> Payments, FeePaymentReceipt Receipt, FinanceExternalBalanceAmounts BalanceAfter);

	public async Task<ExternalCollection> CollectExternalAsync(Charge charge, FinanceExternalPayerIdentity payer,
		string currency, DateOnly schoolReceiptDate, IReadOnlyList<ExternalPayment> payments,
		string key, string fingerprint, string? notes, FinanceExternalChargeBalanceReader balances)
	{
		if (!currentTenant.Id.HasValue || charge.TenantId != currentTenant.Id || charge.StudentId != payer.StudentId ||
			charge.EmployeeId != payer.EmployeeId || currency != "INR" || schoolReceiptDate == default ||
			charge.Status != ChargeStatus.Approved) throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		var amount = FinanceExternalSettlementAmounts.PaymentTotal(payments.Select(row => row.Amount));
		var balance = await balances.ReadAsync(charge);
		if (amount > balance.Amounts.Collectible) throw new BusinessException(LibraryFinanceErrorCodes.InvalidAmount);
		if (payments.Any(row => !Enum.IsDefined(row.Method)) || payments.Any(row => row.Method != PaymentMethodEnum.Cash) && payments.Count != 1)
			throw new BusinessException(FeeCollectionErrorCodes.PaymentDetailsMismatch,
				"Record each bank instrument in a separate collection so it can be verified independently.");
		var provisional = payments.Any(row => row.Method != PaymentMethodEnum.Cash);
		var kind = provisional ? FeeReceiptKind.ProvisionalCharge : FeeReceiptKind.FinalCharge;
		var number = await feeReceiptManager.GenerateReceiptNumberAsync(kind, schoolReceiptDate);
		var metadata = FeeRecordMetadata.CreateExternal(GuidGenerator.Create(), currentTenant.Id.Value, payer,
			charge.AcademicYearId, amount, currency, number,
			provisional ? FeeCollectionStatus.InReconciliation : FeeCollectionStatus.Paid, key, fingerprint, notes);
		var now = new DateTimeOffset(clock.Now).ToUniversalTime();
		var allocation = ChargeCollectionRecord.CreateExternal(GuidGenerator.Create(), currentTenant.Id.Value,
			charge.Id, metadata.Id, payer, amount, now);
		var instruments = payments.Select(row =>
		{
			var record = PaymentMethodRecord.Create(payer.StudentId, metadata.Id, row.Amount, now, row.Method,
				row.Details, currentTenant.Id, notes, employeeId: payer.EmployeeId);
			record.SetReceivingBank(row.BankAccountId, row.BankLabel);
			return record;
		}).ToList();
		var receipt = FeePaymentReceipt.CreateExternal(GuidGenerator.Create(), currentTenant.Id.Value, metadata.Id,
			charge.AcademicYearId, payer, amount, currency, number, schoolReceiptDate, kind, notes);
		await feeRecordMetadataRepository.InsertAsync(metadata, autoSave: false);
		await chargeCollectionRecordRepository.InsertAsync(allocation, autoSave: false);
		await paymentMethodRecordRepository.InsertManyAsync(instruments, autoSave: false);
		await feePaymentReceiptRepository.InsertAsync(receipt, autoSave: false);
		var after = FinanceExternalSettlementAmounts.Balance(charge.Amount, balance.Amounts.Accepted + amount,
			balance.Amounts.Settled + (provisional ? 0m : amount),
			balance.Amounts.PendingVerification + (provisional ? amount : 0m), balance.Amounts.NetWaived, 0m, charge.Status);
		return new(metadata, allocation, instruments, receipt, after);
	}
}
