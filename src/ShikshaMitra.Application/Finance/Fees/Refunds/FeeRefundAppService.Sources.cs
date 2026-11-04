using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
    private async Task<Dictionary<Guid, string>> BuildSourceReceiptMapAsync(IEnumerable<FeeRefundRequest> requests)
    {
        var ids = requests.SelectMany(request => request.Lines.Select(line => line.SourceReceiptId)
            .Append(request.OriginalReceiptId)).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        return ids.Length == 0 ? [] : (await feePaymentReceiptRepository.GetListAsync(receipt => Enumerable.Contains(ids, receipt.Id)))
            .ToDictionary(receipt => receipt.Id, receipt => receipt.ReceiptNumber);
    }

    // Recheck every immutable source at the financial transition, including secondary receipts.
    private async Task ValidateRefundSourceEvidenceAsync(FeeRefundRequest request)
    {
        if (request.WorkflowVersion != 1) return;
        if (request.Lines.Any(line => !line.SourceReceiptId.HasValue || !line.SourceAllocationId.HasValue))
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        var ids = request.Lines.Select(line => line.SourceReceiptId!.Value).Distinct().ToArray();
        var receipts = (await feePaymentReceiptRepository.GetListAsync(receipt => Enumerable.Contains(ids, receipt.Id)))
            .ToDictionary(receipt => receipt.Id);
        if (receipts.Count != ids.Length || receipts.Values.Any(receipt =>
            receipt.FinanceFeeAccountId != request.FinanceFeeAccountId ||
            !request.FinanceFeeAccountId.HasValue && receipt.StudentId != request.StudentId ||
            request.FinanceFeeAccountId.HasValue && receipt.StudentId.HasValue && request.StudentId.HasValue && receipt.StudentId != request.StudentId ||
            receipt.AcademicYearId != request.AcademicYearId || !RefundReceiptEvidencePolicy.IsSettled(receipt)))
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        var collectionIds = receipts.Values.Select(receipt => receipt.FeeCollectionTransactionId).Distinct().ToArray();
        var collections = (await collectionRepository.GetListAsync(collection => Enumerable.Contains(collectionIds, collection.Id)))
            .ToDictionary(collection => collection.Id);
        if (collections.Count != collectionIds.Length || receipts.Values.Any(receipt =>
            collections[receipt.FeeCollectionTransactionId].FinanceFeeAccountId != receipt.FinanceFeeAccountId ||
            collections[receipt.FeeCollectionTransactionId].AcademicYearId != receipt.AcademicYearId ||
            !receipt.FinanceFeeAccountId.HasValue && collections[receipt.FeeCollectionTransactionId].StudentId != receipt.StudentId))
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        if (collections.Values.Any(collection => collection.Status is FeeCollectionStatus.Reversed or FeeCollectionStatus.InReconciliation))
            throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);
        var allocationIds = request.Lines.Where(line => line.DueFeeId.HasValue)
            .Select(line => line.SourceAllocationId!.Value).Distinct().ToArray();
        var allocations = (await allocationRepository.GetListAsync(allocation => Enumerable.Contains(allocationIds, allocation.Id)))
            .ToDictionary(allocation => allocation.Id);
        if (allocations.Count != allocationIds.Length || request.Lines.Where(line => line.DueFeeId.HasValue).Any(line =>
            !RefundReceiptEvidencePolicy.CoversPrincipal(receipts[line.SourceReceiptId!.Value]) ||
            allocations[line.SourceAllocationId!.Value].DueFeeId != line.DueFeeId ||
            allocations[line.SourceAllocationId!.Value].FeeRecordMetadataId != receipts[line.SourceReceiptId!.Value].FeeCollectionTransactionId))
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
    }
}
