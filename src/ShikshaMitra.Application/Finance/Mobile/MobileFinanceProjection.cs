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
	public static MobileFinanceReceipt Receipt(FeePaymentReceipt r) => new(r.Id, r.FeeCollectionTransactionId, r.ReceiptNumber,
		r.ReceiptKind.ToString(), r.VerificationStatus.ToString(), r.ReceiptDate, Money(LegacyMoney(r.TotalPaidAmount)), r.LinkedFinalReceiptId ?? r.LinkedProvisionalReceiptId);
	public static MobileFinanceOverview Overview(Guid studentId, Guid yearId, DateTime today, IReadOnlyList<DueFee> dues,
		IReadOnlyList<MobileChargeBalance> charges, IReadOnlyList<LateFeeBalanceService.Balance> lateFees,
		IReadOnlyList<MobilePaymentBalance> payments)
	{
		var billed = dues.Where(x => x.IsBillGeneratedOn(today)).ToList();
		var pendingAmount = payments.Where(x => x.Status == FeeCollectionStatus.InReconciliation).Sum(x => x.Amount);
		var verifiedPaid = payments.Where(x => x.Status != FeeCollectionStatus.InReconciliation && x.Status != FeeCollectionStatus.Reversed).Sum(x => x.Amount);
		var next = dues.Where(x => x.OutstandingAmount > 0m).OrderBy(x => x.ScheduleDueDate ?? x.DueDate).FirstOrDefault();
		var nextDate = next == null ? (DateTime?)null : (next.ScheduleDueDate ?? next.DueDate).Date;
		return new(studentId, yearId, "INR", new DateTimeOffset(DateTime.SpecifyKind(today, DateTimeKind.Utc)),
			Money(billed.Sum(x => x.OriginalAmount)), Money(verifiedPaid), Money(pendingAmount),
			Money(billed.Sum(x => x.OutstandingAmount)), Money(dues.Where(x => !x.IsBillGeneratedOn(today)).Sum(x => x.OutstandingAmount)),
			Money(lateFees.Sum(x => x.PendingAmount)), Money(charges.Sum(x => x.Outstanding)), Money(dues.Sum(x => x.ConcessionAmount)),
			Money(charges.Sum(x => x.Waived) + lateFees.Sum(x => x.WaivedAmount)), Money(dues.Sum(x => x.AdjustmentCreditAmount)),
			Money(dues.Sum(x => x.RefundedAmount)), nextDate.HasValue ? DateOnly.FromDateTime(nextDate.Value) : null,
			nextDate.HasValue ? Money(dues.Where(x => (x.ScheduleDueDate ?? x.DueDate).Date == nextDate.Value).Sum(x => x.OutstandingAmount)) : null);
	}
}
public record MobileChargeBalance(Guid Id, string Name, string? Reason, decimal Amount, decimal Paid, decimal Waived, decimal ReconciliationAmount = 0m)
{
	public decimal Outstanding => Math.Max(0m, Amount - Paid - Waived);
	public MobileFinanceCharge ToDto() => new(Id, Name, Reason, MobileFinanceProjection.Money(Amount), MobileFinanceProjection.Money(Paid),
		MobileFinanceProjection.Money(Outstanding), ReconciliationAmount > 0m ? "InReconciliation" : Outstanding <= 0m ? Waived > 0m ? "Waived" : "Paid" : Paid > 0m ? "Partial" : "Pending");
}
public record MobilePaymentBalance(Guid Id, decimal Amount, FeeCollectionStatus Status);
