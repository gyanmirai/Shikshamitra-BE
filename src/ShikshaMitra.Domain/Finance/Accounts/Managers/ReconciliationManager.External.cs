using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

public partial class ReconciliationManager
{
	private async Task<FinanceReceiptSettlementEvidence> PrepareTypedVerificationAsync(FeePaymentReceipt receipt,
		CancellationToken cancellationToken)
	{
		var header = await metadataRepository.GetAsync(receipt.FeeCollectionTransactionId, cancellationToken: cancellationToken);
		var allocations = await chargeCollectionRecordRepository.GetListAsync(row => row.FeeRecordMetadataId == header.Id,
			cancellationToken: cancellationToken);
		if (header.TenantId != receipt.TenantId || header.AcademicYearId != receipt.AcademicYearId ||
			header.StudentId != receipt.StudentId || header.EmployeeId != receipt.EmployeeId || header.Currency != "INR" ||
			header.Status != FeeCollectionStatus.InReconciliation || allocations.Count != 1 ||
			allocations[0].Amount != header.TotalPaidFee)
			throw new BusinessException(ReconciliationErrorCodes.InstrumentAttributionRequired);
		var charge = await chargeRepository.GetAsync(allocations.Single().ChargeId, cancellationToken: cancellationToken);
		var before = (await LazyServiceProvider.LazyGetRequiredService<FinanceExternalChargeBalanceReader>().ReadAsync(charge)).Amounts;
		var after = FinanceExternalSettlementAmounts.Balance(charge.Amount, before.Accepted,
			before.Settled + header.TotalPaidFee, before.PendingVerification - header.TotalPaidFee,
			before.NetWaived, 0m, charge.Status);
		if (after.Collectible == 0m) header.MarkPaid(); else header.MarkPartial();
		await metadataRepository.UpdateAsync(header, autoSave: false, cancellationToken: cancellationToken);
		return FinanceReceiptEvidence.FromBalance(after, header.TotalPaidFee);
	}
}
