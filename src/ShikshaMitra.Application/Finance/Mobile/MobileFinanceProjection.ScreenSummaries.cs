using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;

namespace ShikshaMitra.Finance.Mobile;

public static partial class MobileFinanceProjection
{
 public static MobileFinancePaymentSummary PaymentSummary(Guid studentId, Guid yearId,
  IEnumerable<(decimal Amount, FeeCollectionStatus Status, DateOnly Date)> payments)
 {
  var paid=payments.Where(x=>x.Status==FeeCollectionStatus.Paid).ToList();
  return new(studentId,yearId,Money(paid.Sum(x=>x.Amount)),paid.Count,paid.Count==0?null:paid.Max(x=>x.Date));
 }

 public static MobileFinanceRequestSummary ConcessionSummary(Guid studentId,Guid yearId,IReadOnlyList<DueFee> dues) =>
  new(studentId,yearId,"concession",Money(dues.Sum(x=>x.ConcessionAmount)),0);

 public static MobileFinanceRequestSummary RefundSummary(Guid studentId,Guid yearId,
  IEnumerable<(decimal ProcessedAmount,decimal ReversedAmount,FeeRefundStatus Status)> refunds)
 {
  var rows=refunds.ToList();
  return new(studentId,yearId,"refund",Money(rows.Sum(x=>x.ProcessedAmount-x.ReversedAmount)),
   rows.Count(x=>x.Status==FeeRefundStatus.Processed&&x.ProcessedAmount>0m&&x.ReversedAmount==0m));
 }
}
