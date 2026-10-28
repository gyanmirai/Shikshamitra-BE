using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.Authorization;
using Volo.Abp;

namespace ShikshaMitra.Finance.Mobile;

/// <summary>Shared decimal read projection for the family screens and statement.</summary>
public static class MobileFinanceProjection
{
	public static string Money(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);
	public static string InstallmentScheduleStatus(DueFee due, DateTime asOf, decimal reconciliationAmount)
	{
		var status = DueFeeCollectionStatusResolver.Resolve(due, asOf, Convert.ToDouble(reconciliationAmount));
		return !due.IsBillGeneratedOn(asOf) && status is FeeCollectionStatus.Pending or FeeCollectionStatus.Partial
			? "Upcoming" : status.ToString();
	}
	// Versioned and concession-linked requests carry the authoritative policy result separately from selected paid base.
	public static decimal RefundRequestAmount(decimal requestedAmount, decimal calculatedAmount, bool legacy) =>
		legacy ? requestedAmount : calculatedAmount;
	// Legacy storage is converted per row before summation, never reaggregated as binary floating point.
	public static decimal LegacyMoney(double value) => decimal.Round(Convert.ToDecimal(value), 2, MidpointRounding.AwayFromZero);
	public static decimal PaymentAmount(double recordedTotal,IReadOnlyList<decimal> decimalSplits)
	{
		var amount=decimalSplits.Sum();
		if(amount!=LegacyMoney(recordedTotal))throw new BusinessException("Finance:SelfService:PaymentEvidenceIncomplete");
		return amount;
	}
	public static void RequireReceiptOwnership(FeePaymentReceipt receipt, Guid studentId, Guid yearId, Guid? tenantId)
	{
		if (!tenantId.HasValue || receipt.TenantId != tenantId || receipt.StudentId != studentId || receipt.AcademicYearId != yearId)
			throw new AbpAuthorizationException();
	}
	public static MobileFinanceReceipt Receipt(FeePaymentReceipt r) => new(r.Id, r.ReceiptNumber,
 r.ReceiptKind.ToString(), r.ReceiptScope.ToString(), r.VerificationStatus.ToString(), Money(LegacyMoney(r.TotalPaidAmount)),
 r.ReceiptDate, r.CreationTime, r.FeeCollectionTransactionId, true, r.LinkedFinalReceiptId, r.LinkedProvisionalReceiptId, null, null);
 public static MobileFinanceReceiptReference ReceiptReference(MobileFinanceReceipt r) => new(r.Id,r.ReceiptNumber,r.ReceiptScope,r.HasPdf,
 r.ReceiptKind,r.VerificationStatus,r.LinkedFinalReceiptId,r.LinkedProvisionalReceiptId);
 public static IReadOnlyList<(string Label, string Amount)> StatementSummary(MobileFinanceOverview overview)
	{
		var fee = overview.FeeOverview;
		return [("Total fee", fee.TotalFee), ("Net fee", fee.NetFee), ("Paid fee", fee.PaidFee),
			("Pending fee", fee.PendingFee), ("Overdue fee", fee.OverdueFee), ("In Reconciliation", fee.ReconciliationAmount),
			("Concession", fee.ConcessionAmount), ("Recorded fee payments", fee.GrossPaidAmount), ("Refunded", fee.RefundedAmount),
			("Excess fee payment", fee.OverpaymentAmount), ("Adjustment credits", fee.AdjustmentCreditAmount),
			("Future installments", overview.FutureScheduledAmount), ("Additional charge outstanding", overview.ChargeOutstandingAmount),
			("Late fee outstanding", overview.LateFeeAmount), ("Charge and late fee waivers", overview.WaiverAmount)];
	}
	public static MobileFinanceOverview Overview(Guid studentId, Guid yearId, DateTime today, IReadOnlyList<DueFee> dues,
		IReadOnlyList<MobileChargeBalance> charges, IReadOnlyList<LateFeeBalanceService.Balance> lateFees,
		IReadOnlyList<MobilePaymentBalance> payments, decimal principalReconciliationAmount = 0m)
	{
		var principal = FeePrincipalOverview.Calculate(dues, today, principalReconciliationAmount);
		var upcoming = dues.Where(x => !x.IsBillGeneratedOn(today)).Sum(x => x.OutstandingAmount);
		var next = dues.Where(x => x.OutstandingAmount > 0m).OrderBy(x => x.ScheduleDueDate ?? x.DueDate).FirstOrDefault();
		var nextDate = next == null ? (DateTime?)null : (next.ScheduleDueDate ?? next.DueDate).Date;
		return new MobileFinanceOverview(studentId, yearId, "INR", new DateTimeOffset(DateTime.SpecifyKind(today, DateTimeKind.Utc)),
   Money(upcoming),
   Money(lateFees.Sum(x => x.PendingAmount)), Money(charges.Sum(x => x.Outstanding)),
   Money(charges.Sum(x => x.Waived) + lateFees.Sum(x => x.WaivedAmount)), nextDate.HasValue ? DateOnly.FromDateTime(nextDate.Value) : null,
   nextDate.HasValue ? Money(dues.Where(x => (x.ScheduleDueDate ?? x.DueDate).Date == nextDate.Value).Sum(x => x.OutstandingAmount)) : null)
		{
			DueNowAmount = Money(principal.PendingFee - principal.OverdueFee),
			RemainingAmount = Money(principal.PendingFee + upcoming),
			FeeOverview = new(Money(principal.TotalFee), Money(principal.NetFee), Money(principal.PaidFee), Money(principal.PendingFee),
				Money(principal.ConcessionAmount), Money(principal.OverdueFee), Money(principal.ReconciliationAmount), Money(principal.GrossPaidAmount),
				Money(principal.RefundedAmount), Money(principal.OverpaymentAmount), Money(principal.AdjustmentCreditAmount))
		};
	}
}
public record MobileChargeBalance(Guid Id, string Name, string? Reason, decimal Amount, decimal Paid, decimal Waived, decimal ReconciliationAmount = 0m)
{
	public decimal Outstanding => Math.Max(0m, Amount - Paid - Waived);
	public MobileFinanceCharge ToDto() => new(Id, Name, Reason, MobileFinanceProjection.Money(Amount), MobileFinanceProjection.Money(Paid),
		MobileFinanceProjection.Money(Outstanding), ReconciliationAmount > 0m ? "InReconciliation" : Outstanding <= 0m ? Waived > 0m ? "Waived" : "Paid" : Paid > 0m ? "Partial" : "Pending");
}
public record MobilePaymentBalance(Guid Id, decimal Amount, FeeCollectionStatus Status);
