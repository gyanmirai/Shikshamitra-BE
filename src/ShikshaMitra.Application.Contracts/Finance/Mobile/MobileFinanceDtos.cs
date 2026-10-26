using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Mobile;

public record MobileFinanceYear(Guid Id, string Name);
public record MobileFinanceCapabilities(bool CanView, bool CanDownloadReceipts, bool CanDownloadStatement, bool CanRequestConcession, bool CanRequestRefund, bool CanRequestAdjustment, bool OnlinePaymentEnabled = false);
public record MobileFinanceContext(Guid StudentId, string StudentName, IReadOnlyList<MobileFinanceYear> AcademicYears, Guid? SelectedAcademicYearId, MobileFinanceCapabilities Capabilities, Dictionary<string, string> DisabledReasons) { public string Currency => "INR"; }
public record MobileFinanceOverview(Guid StudentId, Guid AcademicYearId, string Currency, DateTimeOffset AsOf,
	string BilledAmount, string VerifiedPaidAmount, string PendingVerificationAmount, string OutstandingAmount,
	string FutureScheduledAmount, string LateFeeAmount, string ChargeOutstandingAmount, string ConcessionAmount,
	string WaiverAmount, string AdjustmentAmount, string RefundedAmount, DateOnly? NextDueDate, string? NextDueAmount);
public record MobileFinanceInstallment(Guid Id, string Label, string FeeHeadName, DateOnly? DueDate, DateOnly? DemandDate, string Amount, string PaidAmount, string OutstandingAmount, string Status) { public string Currency => "INR"; }
public record MobileFinanceCharge(Guid Id, string Name, string? Reason, string Amount, string PaidAmount, string OutstandingAmount, string Status) { public string Currency => "INR"; }
public record MobileFinanceReceipt(Guid Id, Guid? PaymentId, string ReceiptNumber, string Kind, string VerificationStatus, DateOnly Date, string Amount, Guid? LinkedReceiptId) { public string Currency => "INR"; }
public record MobileFinancePayment(Guid Id, DateOnly Date, string Amount, string Status, IReadOnlyList<string> PaymentMethods, IReadOnlyList<MobileFinanceReceipt> Receipts) { public string Currency => "INR"; }
public record MobileFinanceRequest(Guid Id, string Kind, string Title, string Status, string Amount, DateTimeOffset Date, string? Reason, IReadOnlyList<string> AvailableActions) { public string Currency => "INR"; }
public record MobileFinancePage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize) { public string Currency => "INR"; }
