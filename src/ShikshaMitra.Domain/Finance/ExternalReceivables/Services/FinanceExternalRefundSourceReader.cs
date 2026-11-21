using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalRefundSourceReader(FinanceExternalChargeBalanceReader balances,
	IRepository<FeePaymentReceipt, Guid> receipts, IFeeRefundRepository refunds,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant) : DomainService
{
	public async Task<List<PaidChargeRefundSource>> ReadAsync(FinanceExternalReceivableSourceLink link,
		Charge charge, Guid? excludingRequestId = null)
	{
		if (!tenant.Id.HasValue || link.TenantId != tenant.Id || charge.TenantId != tenant.Id ||
			link.FinanceChargeId != charge.Id || link.AcademicYearId != charge.AcademicYearId)
			throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
		if (charge.Status != ChargeStatus.Approved) return [];
		var ledger = await balances.ReadAsync(charge);
		var settledIds = ledger.Collections.Where(row => row.Status is FeeCollectionStatus.Paid or
			FeeCollectionStatus.Partial).Select(row => row.Id).ToArray();
		if (settledIds.Length == 0) return [];
		var evidence = await receipts.GetListAsync(row => row.TenantId == tenant.Id &&
			Enumerable.Contains(settledIds, row.FeeCollectionTransactionId) && row.AcademicYearId == charge.AcademicYearId &&
			row.StudentId == charge.StudentId && row.EmployeeId == charge.EmployeeId && !row.FinanceFeeAccountId.HasValue);
		var byCollection = evidence.Where(row => RefundReceiptEvidencePolicy.IsSettled(row) && RefundReceiptEvidencePolicy.CoversCharges(row))
			.GroupBy(row => row.FeeCollectionTransactionId).ToDictionary(group => group.Key,
				group => group.OrderByDescending(row => row.ReceiptKind is FeeReceiptKind.FinalCharge or FeeReceiptKind.FinalFee)
					.ThenByDescending(row => row.CreationTime).ThenBy(row => row.Id).First());
		var allocations = ledger.Allocations.Where(row => byCollection.ContainsKey(row.FeeRecordMetadataId)).ToList();
		foreach (var (collectionId, receipt) in byCollection)
			EnsureReceiptEvidence(charge, ledger.Collections.Single(row => row.Id == collectionId), receipt,
				allocations.Where(row => row.FeeRecordMetadataId == collectionId).Sum(row => row.Amount));
		var ids = allocations.Select(row => row.Id).ToArray();
		var committed = await refunds.GetCommittedAllocationAmountsAsync(ids, excludingRequestId);
		var pending = await queries.ToListAsync((await refunds.GetQueryableAsync()).Where(request => request.TenantId == tenant.Id &&
			request.FinanceExternalSourceLinkId == link.Id && (!excludingRequestId.HasValue || request.Id != excludingRequestId.Value) &&
			(request.Status == FeeRefundStatus.Submitted || request.Status == FeeRefundStatus.UnderReview))
			.SelectMany(request => request.Lines).Where(line => line.SourceAllocationId.HasValue &&
				Enumerable.Contains(ids, line.SourceAllocationId.Value)).Select(line => new { line.SourceAllocationId, line.CalculatedAmount }));
		foreach (var line in pending)
			committed[line.SourceAllocationId!.Value] = committed.GetValueOrDefault(line.SourceAllocationId.Value) + line.CalculatedAmount;
		return allocations.Select(row =>
		{
			var receipt = byCollection[row.FeeRecordMetadataId];
			return new PaidChargeRefundSource(charge.Id, charge.ChargeName, charge.Amount, row.Id, receipt.Id,
				receipt.ReceiptNumber, receipt.ReceiptDate, row.Amount, committed.GetValueOrDefault(row.Id));
		}).ToList();
	}
}
