using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using System.Linq.Expressions;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>Canonical read status for fee obligations, shared by ledger and summary queries.</summary>
public static class DueFeeCollectionStatusResolver
{
	public static Expression<Func<DueFee, bool>> OverdueOn(DateTime asOf)
	{
		var date = asOf.Date;
		return due => due.OutstandingAmount > 0m
			&& (due.ScheduleSource == ScheduleSource.Legacy || !due.DemandDate.HasValue || due.DemandDate.Value.Date <= date)
			&& (due.ScheduleDueDate ?? due.DueDate) < date;
	}

	public static FeeCollectionStatus Resolve(DueFee due, DateTime asOf, double reconciliationAmount)
	{
		// Uncleared allocations already reduce OutstandingAmount. An overdue remainder
		// still needs collection; a fully provisionally settled bill awaits clearance.
		if (due.OutstandingAmount > 0m && due.IsBillGeneratedOn(asOf)
			&& (due.ScheduleDueDate ?? due.DueDate).Date < asOf.Date)
			return FeeCollectionStatus.Overdue;
		if (reconciliationAmount > 0d)
			return FeeCollectionStatus.InReconciliation;
		if (due.OutstandingAmount <= 0m)
			return FeeCollectionStatus.Paid;
		return due.PaidAmount - due.RefundedAmount > 0m || due.ConcessionAmount > 0m || due.AdjustmentCreditAmount > 0m
			? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending;
	}
}
