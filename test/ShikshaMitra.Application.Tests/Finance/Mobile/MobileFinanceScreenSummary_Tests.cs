using System;
using ShikshaMitra.Finance.Mobile;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance;
public class MobileFinanceScreenSummary_Tests
{
 [Fact] public void Payments_summary_excludes_uncleared_and_reversed_money_and_keeps_paise()
 {
  var summary=MobileFinanceProjection.PaymentSummary(Guid.NewGuid(),Guid.NewGuid(),[
   (100.10m,FeeCollectionStatus.Paid,new(2026,9,25)),(20.02m,FeeCollectionStatus.Paid,new(2026,9,20)),
   (900m,FeeCollectionStatus.InReconciliation,new(2026,10,25)),(500m,FeeCollectionStatus.Reversed,new(2026,10,26))]);
  summary.PaidAmount.ShouldBe("120.12");summary.SuccessfulPaymentCount.ShouldBe(2);summary.LatestPaymentDate.ShouldBe(new DateOnly(2026,9,25));
 }
 [Fact] public void Refund_summary_is_retained_payout_with_completed_requests_separate()
 {
  var summary=MobileFinanceProjection.RefundSummary(Guid.NewGuid(),Guid.NewGuid(),[
   (500.25m,0m,FeeRefundStatus.Processed),(200.20m,50.10m,FeeRefundStatus.Approved),
   (100m,100m,FeeRefundStatus.Reversed),(0m,0m,FeeRefundStatus.Submitted)]);
  summary.TotalAmount.ShouldBe("650.35");summary.CompletedRequestCount.ShouldBe(1);
 }
 [Fact] public void Concession_summary_uses_applied_ledger_and_zero_summaries_are_real_zero()
 {
  var child=Guid.NewGuid();var year=Guid.NewGuid();
  var due=new DueFee(Guid.NewGuid(),Guid.NewGuid(),child,Guid.NewGuid(),MonthEnum.October,2026,false,1000m,DateTime.Today);
  due.ApplyConcession(100.25m);
  MobileFinanceProjection.ConcessionSummary(child,year,[due]).TotalAmount.ShouldBe("100.25");
  var payments=MobileFinanceProjection.PaymentSummary(child,year,[]);
  payments.PaidAmount.ShouldBe("0.00");payments.SuccessfulPaymentCount.ShouldBe(0);payments.LatestPaymentDate.ShouldBeNull();
  MobileFinanceProjection.RefundSummary(child,year,[]).TotalAmount.ShouldBe("0.00");
  MobileFinanceProjection.ConcessionSummary(child,year,[]).TotalAmount.ShouldBe("0.00");
 }
}
