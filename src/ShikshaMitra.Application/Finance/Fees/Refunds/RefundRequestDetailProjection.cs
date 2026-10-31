using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

internal sealed record RefundDueDisplayFacts(decimal Total, int Status);

internal static class RefundRequestDetailProjection
{
    public static void Populate(FeeRefundRequestDto dto,
        IReadOnlyDictionary<Guid, RefundDueDisplayFacts>? dueFacts = null)
    {
        dto.RefundRequestId = dto.Id;
        dto.FeeHeads = dto.Lines.GroupBy(line => new
        {
            Kind = line.ChargeId.HasValue ? 1 : line.LateFeeDueFeeId.HasValue ? 2 : 0,
            HeadId = line.ChargeId.HasValue || line.LateFeeDueFeeId.HasValue ? null : line.FeeHeadId,
            SavedHeadName = !line.ChargeId.HasValue && !line.LateFeeDueFeeId.HasValue && !line.FeeHeadId.HasValue
                ? line.FeeHeadName : null
        }).Select(group => new RefundFeeHeadDto
        {
            FeeHeadId = group.Key.HeadId,
            FeeHeadName = group.Key.Kind switch
            {
                1 => "Paid charges",
                2 => "Paid late fees",
                _ => group.Select(line => line.FeeHeadName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Fee head"
            },
            TotalPaid = Convert.ToDouble(group.Sum(line => line.SettledPaidAmount)),
            TotalRefunded = Convert.ToDouble(group.Sum(line => line.ProcessedAmount - line.ReversedAmount)),
            Items = group.GroupBy(line => line.DueFeeId ?? line.ChargeId ?? line.LateFeeDueFeeId ?? line.Id)
                .Select(installment =>
            {
                var line = installment.First();
                var source = line.DueFeeId is Guid dueId && dueFacts != null
                    && dueFacts.TryGetValue(dueId, out var facts) ? facts : null;
                return new RefundMonthItemDto
                {
                    InstallmentId = line.DueFeeId ?? line.ChargeId ?? line.LateFeeDueFeeId ?? line.Id,
                    Month = string.IsNullOrWhiteSpace(line.InstallmentLabel) ? "Installment unavailable" : line.InstallmentLabel,
                    Total = Convert.ToDouble(source?.Total ?? installment.Sum(item => item.SettledPaidAmount)),
                    Paid = Convert.ToDouble(installment.Sum(item => item.SettledPaidAmount)),
                    RefundAmount = Convert.ToDouble(installment.Sum(item => DisplayAmount(dto.Status, item))),
                    Status = source?.Status ?? (int)FeeStatus.Paid
                };
            }).ToList()
        }).ToList();
        dto.TotalFeeHeads = dto.FeeHeads.Count;
        dto.TotalPaidAmount = Convert.ToDouble(dto.Lines.Sum(line => line.SettledPaidAmount));
        dto.TotalRefundedAmount = Convert.ToDouble(dto.ProcessedAmount - dto.ReversedAmount);
    }

    private static decimal DisplayAmount(FeeRefundStatus status, FeeRefundLineDto line) => status switch
    {
        FeeRefundStatus.Approved or FeeRefundStatus.Processing or FeeRefundStatus.Failed or FeeRefundStatus.Reversed => line.ApprovedAmount,
        FeeRefundStatus.Processed => line.ProcessedAmount,
        _ => line.CalculatedAmount
    };
}
