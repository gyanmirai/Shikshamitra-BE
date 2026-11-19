using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Mobile;

public record MobileFamilyCharge(Guid ChargeLineId, Guid? ChargeId, string ChargeName, string? PurposeDescription, Guid ChargeCategoryId, string CategoryName,
	DateOnly DueDate, DateTimeOffset AppliedOn, DateTimeOffset? ApprovedOn, DateTimeOffset? VoidedOn,
	string Status, string PaymentStatus, bool IsOverdue, string Amount, string CollectedAmount, string VerifiedPaidAmount,
	string ReconciliationAmount, string WaivedAmount, string OutstandingAmount, string RefundedAmount)
{ public string Currency => "INR"; }
public record MobileChargeSummary(string Amount, string CollectedAmount, string VerifiedPaidAmount, string ReconciliationAmount,
	string WaivedAmount, string OutstandingAmount, string RefundedAmount);
public record MobileChargeCounts(int All, int Open, int Settled, int Voided);
public record MobileChargeCategory(Guid Id, string Name);
public record MobileChargePage(Guid StudentId, Guid AcademicYearId, DateTimeOffset AsOf,
	IReadOnlyList<MobileFamilyCharge> Items, int TotalCount, int CurrentPage, int PageSize,
	MobileChargeSummary Summary, MobileChargeCounts Counts, IReadOnlyList<MobileChargeCategory> Categories)
{ public string Currency => "INR"; }
public record MobileChargeDetail(Guid StudentId, Guid AcademicYearId, DateTimeOffset AsOf, MobileFamilyCharge Charge)
{ public string Currency => "INR"; }
public record MobileChargeReceipt(Guid ReceiptId, Guid PaymentId, string ReceiptNumber, string ReceiptKind,
	string VerificationStatus, DateOnly ReceiptDate, string TotalPaidAmount, Guid? LinkedFinalReceiptId, Guid? LinkedProvisionalReceiptId, string Scope, bool HasPdf)
{ public string Currency => "INR"; }
public record MobileChargeRefund(Guid Id, string Status, string ProcessedAmount) { public string Currency => "INR"; }
public record MobileChargePayment(Guid SourceAllocationId, Guid PaymentId, DateTimeOffset Date, string AmountPaid, string Status,
	IReadOnlyList<string> PaymentMethods, string RefundedAmount, IReadOnlyList<MobileChargeReceipt> Receipts,
	IReadOnlyList<MobileChargeRefund> Refunds) { public string Currency => "INR"; }
public record MobileChargePaymentPage(Guid StudentId, Guid AcademicYearId, Guid ChargeLineId,
	IReadOnlyList<MobileChargePayment> Items, int TotalCount, int CurrentPage, int PageSize)
{ public string Currency => "INR"; }
