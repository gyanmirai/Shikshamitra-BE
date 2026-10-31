using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public sealed record PaidLateFeeRefundSource(Guid DueFeeId, string Name,
    Guid AllocationId, Guid ReceiptId, string ReceiptNumber, DateOnly ReceiptDate,
    decimal Paid, decimal Committed)
{
    public decimal Available => Math.Max(0m, Paid - Committed);
}

/// <summary>Reads immutable late-fee payment allocations without consuming principal balances.</summary>
public class PaidLateFeeRefundService(IDueFeeRepository dues,
    IRepository<FeeHead, Guid> heads,
    IRepository<LateFeeCollectionAllocation, Guid> allocations,
    IRepository<FeeRecordMetadata, Guid> collections,
    IRepository<FeePaymentReceipt, Guid> receipts,
    IFeeRefundRepository refunds) : DomainService
{
    public async Task<List<PaidLateFeeRefundSource>> GetSourcesAsync(Guid? studentId, Guid academicYearId,
        Guid? accountId = null, Guid? receiptId = null, Guid? excludingRequestId = null, Guid? assignmentId = null)
    {
        if (!studentId.HasValue && !accountId.HasValue) return [];
        var sourceDues = await dues.GetListAsync(x => accountId.HasValue
            ? x.FinanceFeeAccountId == accountId
            : x.StudentId == studentId && x.StudentFeeStructureId == assignmentId);
        var dueIds = sourceDues.Select(x => x.Id).ToList();
        var settled = await collections.GetListAsync(x => x.AcademicYearId == academicYearId &&
            (accountId.HasValue ? x.FinanceFeeAccountId == accountId : x.StudentId == studentId && !x.FinanceFeeAccountId.HasValue) &&
            x.Status != FeeCollectionStatus.Reversed && x.Status != FeeCollectionStatus.InReconciliation);
        var collectionIds = settled.Select(x => x.Id).ToList();
        var evidence = await receipts.GetListAsync(x => collectionIds.Contains(x.FeeCollectionTransactionId) &&
            x.AcademicYearId == academicYearId && (accountId.HasValue ? x.FinanceFeeAccountId == accountId
                : x.StudentId == studentId && !x.FinanceFeeAccountId.HasValue) &&
            (!receiptId.HasValue || x.Id == receiptId) &&
            (x.ReceiptKind == FeeReceiptKind.FinalFee || x.ReceiptKind == FeeReceiptKind.FinalCharge ||
                (x.ReceiptKind == FeeReceiptKind.ProvisionalFee || x.ReceiptKind == FeeReceiptKind.ProvisionalCharge) &&
                x.VerificationStatus == FeeReceiptVerificationStatus.Verified));
        // The amount snapshot lives in ExtraProperties, so attribution is evaluated after materialization.
        var receiptMap = evidence.Where(RefundReceiptEvidencePolicy.CoversLateFees)
            .GroupBy(x => x.FeeCollectionTransactionId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(x => x.ReceiptKind is FeeReceiptKind.FinalFee or FeeReceiptKind.FinalCharge)
                .ThenByDescending(x => x.CreationTime).First());
        var sourceCollectionIds = receiptMap.Keys.ToList();
        var paid = await allocations.GetListAsync(x => dueIds.Contains(x.DueFeeId) &&
            sourceCollectionIds.Contains(x.FeeRecordMetadataId) && x.Amount > 0);
        var committed = await refunds.GetCommittedAllocationAmountsAsync(paid.Select(x => x.Id).ToList(), excludingRequestId);
        var headIds = sourceDues.Select(x => x.FeeHeadId).Distinct().ToList();
        var headNames = (await heads.GetListAsync(x => headIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
        var dueMap = sourceDues.ToDictionary(x => x.Id);
        return paid.Select(x =>
        {
            var receipt = receiptMap[x.FeeRecordMetadataId];
            var due = dueMap[x.DueFeeId];
            return new PaidLateFeeRefundSource(due.Id,
                $"Late Fee - {headNames.GetValueOrDefault(due.FeeHeadId, "Fee head")} ({due.GetDisplayInstallmentLabel()})",
                x.Id, receipt.Id, receipt.ReceiptNumber, receipt.ReceiptDate,
                Convert.ToDecimal(x.Amount), committed.GetValueOrDefault(x.Id));
        }).ToList();
    }
}
