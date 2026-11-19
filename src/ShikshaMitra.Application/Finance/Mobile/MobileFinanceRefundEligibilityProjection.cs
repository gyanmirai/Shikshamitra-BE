using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
namespace ShikshaMitra.Finance.Mobile;
public static class MobileFinanceRefundEligibilityProjection
{
 public static MobileRefundEligibility Map(RefundEligibilityDto result) => MobileFinanceCanonicalCommandProjection.Map(result);
}
