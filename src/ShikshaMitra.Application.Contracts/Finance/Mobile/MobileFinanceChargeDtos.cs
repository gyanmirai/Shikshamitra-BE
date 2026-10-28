using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Mobile;

public record MobileFamilyCharge(Guid Id, string Name, string? Reason, Guid CategoryId, string CategoryName,
	DateOnly DueDate, DateTimeOffset AppliedOn, DateTimeOffset? ApprovedOn, DateTimeOffset? VoidedOn,
	string DecisionStatus, string PaymentStatus, bool IsOverdue, string Amount, string VerifiedPaidAmount,
	string PendingVerificationAmount, string WaivedAmount, string OutstandingAmount, string RefundedAmount)
{ public string Currency => "INR"; }
public record MobileChargeSummary(string Amount, string VerifiedPaidAmount, string PendingVerificationAmount,
	string WaivedAmount, string OutstandingAmount, string RefundedAmount);
public record MobileChargeCounts(int All, int Open, int Settled, int Voided);
public record MobileChargeCategory(Guid Id, string Name);
public record MobileChargePage(Guid StudentId, Guid AcademicYearId, DateTimeOffset AsOf,
	IReadOnlyList<MobileFamilyCharge> Items, int TotalCount, int Page, int PageSize,
	MobileChargeSummary Summary, MobileChargeCounts Counts, IReadOnlyList<MobileChargeCategory> Categories)
{ public string Currency => "INR"; }
public record MobileChargeDetail(Guid StudentId, Guid AcademicYearId, DateTimeOffset AsOf, MobileFamilyCharge Charge)
{ public string Currency => "INR"; }
public record MobileChargeReceipt(Guid Id, Guid PaymentId, string ReceiptNumber, string Kind,
	string VerificationStatus, DateOnly Date, string Amount, Guid? LinkedReceiptId, string Scope)
{ public string Currency => "INR"; }
public record MobileChargeRefund(Guid Id, string Status, string Amount) { public string Currency => "INR"; }
public record MobileChargePayment(Guid Id, Guid PaymentId, DateTimeOffset Date, string Amount, string Status,
	IReadOnlyList<string> PaymentMethods, string RefundedAmount, IReadOnlyList<MobileChargeReceipt> Receipts,
	IReadOnlyList<MobileChargeRefund> Refunds) { public string Currency => "INR"; }
public record MobileChargePaymentPage(Guid StudentId, Guid AcademicYearId, Guid ChargeId,
	IReadOnlyList<MobileChargePayment> Items, int TotalCount, int Page, int PageSize)
{ public string Currency => "INR"; }
