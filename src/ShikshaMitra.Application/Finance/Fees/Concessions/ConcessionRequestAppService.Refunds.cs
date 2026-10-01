using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.Concessions;

public partial class ConcessionRequestAppService
{
	private async Task<List<FeeRefundRequest>> CreateConcessionRefundRequestsAsync(
		ConcessionRequest request,
		IReadOnlyDictionary<Guid, decimal> newOverpaymentByDueId,
		IReadOnlyDictionary<Guid, DueFee> dueFees)
	{
		var targets = newOverpaymentByDueId.Where(x => x.Value > 0m).ToList();
		if (targets.Count == 0) return [];

		var dueIds = targets.Select(x => x.Key).ToList();
		var allocations = await paymentAllocationRepository.GetListAsync(x => dueIds.Contains(x.DueFeeId));
		var transactionIds = allocations.Select(x => x.FeeRecordMetadataId).Distinct().ToList();
		var transactions = (await feeRecordMetadataRepository.GetListAsync(x => transactionIds.Contains(x.Id)))
			.Where(x => x.StudentId == request.StudentId && x.AcademicYearId == request.AcademicYearId &&
				x.Status is not (FeeCollectionStatus.Reversed or FeeCollectionStatus.InReconciliation))
			.ToDictionary(x => x.Id);
		var receipts = (await paymentReceiptRepository.GetListAsync(x =>
			transactionIds.Contains(x.FeeCollectionTransactionId) &&
			x.StudentId == request.StudentId && x.AcademicYearId == request.AcademicYearId &&
			x.ReceiptScope == FeeReceiptScope.Fee))
			.Where(x => x.ReceiptKind == FeeReceiptKind.FinalFee ||
				(x.ReceiptKind == FeeReceiptKind.ProvisionalFee &&
				 x.VerificationStatus == FeeReceiptVerificationStatus.Verified))
			.GroupBy(x => x.FeeCollectionTransactionId)
			.ToDictionary(x => x.Key, x => x.OrderByDescending(r => r.ReceiptKind == FeeReceiptKind.FinalFee).First());
		var allocationIds = allocations.Select(x => x.Id).ToList();
		var committed = await feeRefundRepository.GetCommittedAllocationAmountsAsync(allocationIds, null);
		var reserved = await feeRefundRepository.GetActiveReservedAmountsAsync(dueIds, null);
		var linesByReceipt = new Dictionary<Guid, (FeePaymentReceipt Receipt, List<RefundLineSpec> Lines)>();

		foreach (var target in targets)
		{
			var due = dueFees[target.Key];
			var remaining = Math.Min(target.Value,
				Math.Max(0m, due.OverpaymentAmount - reserved.GetValueOrDefault(due.Id)));
			if (remaining != target.Value)
				throw new BusinessException(FinanceConcessionErrorCodes.RefundSourceUnavailable)
					.WithData("DueFeeId", due.Id);
			foreach (var allocation in allocations.Where(x => x.DueFeeId == due.Id)
				.OrderBy(x => x.CreationTime).ThenBy(x => x.Id))
			{
				if (!transactions.ContainsKey(allocation.FeeRecordMetadataId) ||
					!receipts.TryGetValue(allocation.FeeRecordMetadataId, out var receipt)) continue;
				var available = Math.Max(0m, Convert.ToDecimal(allocation.Amount) -
					committed.GetValueOrDefault(allocation.Id));
				var amount = Math.Min(remaining, available);
				if (amount <= 0m) continue;
				if (!linesByReceipt.TryGetValue(receipt.Id, out var group))
					group = (receipt, []);
				group.Lines.Add(new RefundLineSpec(due.Id, allocation.Id, receipt.Id,
					due.FeeHeadId, allocation.FeeHeadName ?? "Fee head",
					due.GetDisplayInstallmentLabel(), available, amount, amount));
				linesByReceipt[receipt.Id] = group;
				remaining -= amount;
				if (remaining == 0m) break;
			}
			if (remaining > 0m)
				throw new BusinessException(FinanceConcessionErrorCodes.RefundSourceUnavailable)
					.WithData("DueFeeId", due.Id)
					.WithData("Amount", remaining);
		}

		var created = new List<FeeRefundRequest>();
		foreach (var group in linesByReceipt.Values.OrderBy(x => x.Receipt.Id))
		{
			var snapshot = JsonSerializer.Serialize(new
			{
				Source = "ApprovedConcession", request.Id, request.ConcessionPolicyVersionId,
				ReceiptId = group.Receipt.Id,
				Lines = group.Lines.OrderBy(x => x.DueFeeId).Select(x => new
				{
					x.DueFeeId, x.SourceAllocationId, x.Amount
				})
			});
			var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant();
			created.Add(await feeRefundManager.CreateFromConcessionAsync(request.StudentId,
				request.AcademicYearId, request.StudentFeeStructureId, request.Id,
				group.Receipt.Id, group.Receipt.ReceiptNumber, group.Lines, snapshot, hash,
				currentUser.GetId()));
		}
		return created;
	}

	private async Task EnsureConcessionRefundReversalAllowedAsync(
		ConcessionRequest request, decimal requestedReversal)
	{
		var refunds = await feeRefundRepository.GetListAsync(x => x.ConcessionRequestId == request.Id);
		if (refunds.Count == 0) return;
		if (requestedReversal != request.GetNetApprovedConcession() ||
			refunds.Any(x => x.Status is not (FeeRefundStatus.Cancelled or FeeRefundStatus.Rejected or FeeRefundStatus.Failed or FeeRefundStatus.Reversed)))
			throw new BusinessException(FinanceConcessionErrorCodes.RefundRequestBlocksReversal)
				.WithData("ConcessionRequestId", request.Id);
	}
}
