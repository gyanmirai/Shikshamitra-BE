using ShikshaMitra.Finance.Fees.FeeCollection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>Annual principal facts shared by staff Fee Details and the authorized family reader.</summary>
internal record FeePrincipalOverview(decimal TotalFee, decimal NetFee, decimal PaidFee, decimal PendingFee,
	decimal ConcessionAmount, decimal OverdueFee, decimal ReconciliationAmount, decimal GrossPaidAmount,
	decimal RefundedAmount, decimal OverpaymentAmount, decimal AdjustmentCreditAmount)
{
	public static FeePrincipalOverview Calculate(IEnumerable<DueFee> source, DateTime asOf, decimal reconciliationAmount)
	{
		var dues = source.ToList();
		return new(dues.Sum(d => d.OriginalAmount), dues.Sum(d => d.NetPayableAmount),
			dues.Sum(d => d.AppliedPaymentAmount), dues.Where(d => d.IsBillGeneratedOn(asOf)).Sum(d => d.OutstandingAmount),
			dues.Sum(d => d.ConcessionAmount), dues.Where(DueFeeCollectionStatusResolver.OverdueOn(asOf).Compile()).Sum(d => d.OutstandingAmount),
			reconciliationAmount, dues.Sum(d => d.PaidAmount), dues.Sum(d => d.RefundedAmount),
			dues.Sum(d => d.OverpaymentAmount), dues.Sum(d => d.AdjustmentCreditAmount));
	}
}
