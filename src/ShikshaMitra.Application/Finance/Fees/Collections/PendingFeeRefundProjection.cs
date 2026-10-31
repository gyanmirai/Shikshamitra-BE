using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Fees.FeeRefund;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>Approved principal refunds awaiting payout, separate from money already refunded.</summary>
internal static class PendingFeeRefundProjection
{
	internal static Dictionary<Guid, (decimal Amount, string Description)> Build(IEnumerable<FeeRefundRequest> requests)
	{
		return requests.Where(r => r.WorkflowVersion == 1 &&
				r.Status is FeeRefundStatus.Approved or FeeRefundStatus.Processing)
			.SelectMany(r => r.Lines.Where(l => l.DueFeeId.HasValue).Select(l => new
			{
				DueFeeId = l.DueFeeId!.Value,
				Amount = Math.Max(0m, l.ApprovedAmount - l.ProcessedAmount),
				Description = string.IsNullOrWhiteSpace(r.Reason) ? r.VoucherNumber : $"{r.VoucherNumber}: {r.Reason}",
			}))
			.Where(l => l.Amount > 0m)
			.GroupBy(l => l.DueFeeId)
			.ToDictionary(g => g.Key, g => (
				Amount: g.Sum(l => l.Amount),
				Description: string.Join("; ", g.Select(l => l.Description).Distinct())));
	}
}
