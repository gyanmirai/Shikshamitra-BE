using System.Linq;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

namespace ShikshaMitra.Finance.Mobile;

public static class MobileFinanceRefundEligibilityProjection
{
	public static object Map(RefundEligibilityDto result) => new
	{
		Currency = "INR",
		PolicyName = result.RefundPolicyName, AllowedMethods = result.AllowedPayoutMethods,
		result.AllowPartialRefund, MaximumAllowedAmount = MobileFinanceCommandRules.Money(result.TotalRefundable),
		Receipts = result.Receipts.Select(r => new { Id = r.ReceiptId, r.ReceiptNumber, result.AllowPartialRefund,
			Lines = result.FeeHeads.SelectMany(h => h.Months.Select(x => new { h.FeeHeadName, Row = x }))
				.Where(x => x.Row.SourceReceiptId == r.ReceiptId).Select(x => new { x.Row.DueFeeId, x.Row.SourceAllocationId,
					Label = x.FeeHeadName + " - " + x.Row.Month + " " + x.Row.Year,
					AvailableAmount = MobileFinanceCommandRules.Money(x.Row.Refundable) }).ToArray() }).ToArray()
	};
}
