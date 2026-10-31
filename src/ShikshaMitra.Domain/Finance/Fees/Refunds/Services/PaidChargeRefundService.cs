using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public sealed record PaidChargeRefundSource(Guid ChargeId, string ChargeName, decimal Total,
    Guid AllocationId, Guid ReceiptId, string ReceiptNumber, DateOnly ReceiptDate,
    decimal Paid, decimal Committed)
{
    public decimal Available => Math.Max(0m, Paid - Committed);
}

/// <summary>Reads settled charge allocations and their canonical refund commitments.</summary>
public class PaidChargeRefundService(IRepository<Charge, Guid> charges,
    IRepository<ChargeCollectionRecord, Guid> allocations,
    IRepository<FeeRecordMetadata, Guid> collections,
    IRepository<FeePaymentReceipt, Guid> receipts,
    IFeeRefundRepository refunds) : DomainService
{
    public async Task<List<PaidChargeRefundSource>> GetSourcesAsync(Guid? studentId, Guid academicYearId,
        Guid? accountId = null, Guid? receiptId = null, Guid? excludingRequestId = null)
    {
        if (!studentId.HasValue) return [];
        var eligibleCharges = await charges.GetListAsync(x => x.StudentId == studentId &&
            x.AcademicYearId == academicYearId && x.Status == ChargeStatus.Approved);
        var chargeIds = eligibleCharges.Select(x => x.Id).ToList();
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
        var receiptMap = evidence.Where(RefundReceiptEvidencePolicy.CoversCharges)
            .GroupBy(x => x.FeeCollectionTransactionId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(x => x.ReceiptKind is FeeReceiptKind.FinalFee or FeeReceiptKind.FinalCharge)
                .ThenByDescending(x => x.CreationTime).First());
        var sourceCollectionIds = receiptMap.Keys.ToList();
        var paid = await allocations.GetListAsync(x => x.StudentId == studentId && chargeIds.Contains(x.ChargeId) &&
            sourceCollectionIds.Contains(x.FeeRecordMetadataId) && x.Amount > 0);
        var committed = await refunds.GetCommittedAllocationAmountsAsync(paid.Select(x => x.Id).ToList(), excludingRequestId);
        var chargeMap = eligibleCharges.ToDictionary(x => x.Id);
        return paid.Select(x =>
        {
            var receipt = receiptMap[x.FeeRecordMetadataId];
            var charge = chargeMap[x.ChargeId];
            return new PaidChargeRefundSource(charge.Id, charge.ChargeName, charge.Amount, x.Id,
                receipt.Id, receipt.ReceiptNumber, receipt.ReceiptDate, Convert.ToDecimal(x.Amount), committed.GetValueOrDefault(x.Id));
        }).ToList();
    }
}
