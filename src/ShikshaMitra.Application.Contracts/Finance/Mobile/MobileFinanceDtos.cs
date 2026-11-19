using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Mobile;

public record MobileFinanceYear(Guid Id, string Name);
public record MobileFinanceCapabilities(bool CanView, bool CanDownloadReceipts, bool CanDownloadStatement, bool CanRequestConcession, bool CanRequestRefund, bool CanRequestAdjustment, bool OnlinePaymentEnabled = false,
	bool CanViewCharges = false, bool CanViewReceipts = false, bool CanViewConcessions = false, bool CanViewRefunds = false, bool CanViewAdjustments = false);
public record MobileFinanceContext(Guid StudentId, string StudentName, IReadOnlyList<MobileFinanceYear> AcademicYears, Guid? SelectedAcademicYearId, MobileFinanceCapabilities Capabilities, Dictionary<string, string> DisabledReasons) { public string Currency => "INR"; }
public record MobileFinanceOverview(Guid StudentId, Guid AcademicYearId, string Currency, DateTimeOffset AsOf,
 string FutureScheduledAmount, string LateFeeAmount, string ChargeOutstandingAmount,
 string WaiverAmount, DateOnly? NextDueDate, string? NextDueAmount)
{
 public MobileFeeOverview FeeOverview { get; init; } = MobileFeeOverview.Empty;
 public string? DueNowAmount { get; init; }
 public string? RemainingAmount { get; init; }
}
public record MobileFeeOverview(string TotalFee, string NetFee, string PaidFee, string PendingFee,
	string ConcessionAmount, string OverdueFee, string ReconciliationAmount, string GrossPaidAmount,
	string RefundedAmount, string OverpaymentAmount, string AdjustmentCreditAmount)
{
	public static MobileFeeOverview Empty => new("0.00", "0.00", "0.00", "0.00", "0.00", "0.00", "0.00", "0.00", "0.00", "0.00", "0.00");
}
public record MobileFinanceInstallment(Guid DueFeeId, Guid? InstallmentId, string InstallmentLabel, Guid FeeHeadId, string FeeHeadName,
 DateOnly? DueDate, DateOnly? DemandDate, string Total, string Paid, string Due, string ReconciliationAmount, string ConcessionAmount, string RefundAmount, string Status)
{
 public string Currency => "INR";
 public string? ScheduleStatus { get; init; }
}
public record MobileFinanceCharge(Guid ChargeLineId, string ChargeName, string? PurposeDescription, string Amount, string CollectedAmount, string OutstandingAmount, string PaymentStatus) { public string Currency => "INR"; }
public record MobileFinanceReceipt(Guid Id, string ReceiptNumber, string ReceiptKind, string ReceiptScope, string VerificationStatus,
 string TotalPaidAmount, DateOnly ReceiptDate, DateTime CreationTime, Guid? FeeCollectionTransactionId, bool HasPdf,
 Guid? LinkedFinalReceiptId, Guid? LinkedProvisionalReceiptId, string? LinkedFinalReceiptNumber, string? LinkedProvisionalReceiptNumber) { public string Currency => "INR"; }
public record MobileFinanceReceiptReference(Guid ReceiptId, string ReceiptNumber, string Scope, bool HasPdf,
 string ReceiptKind, string VerificationStatus, Guid? LinkedFinalReceiptId, Guid? LinkedProvisionalReceiptId);
public record MobileFinancePayment(Guid PaymentId, string? ReceiptNumber, DateOnly Date, string Amount, string Status, IReadOnlyList<string> PaymentMethods, IReadOnlyList<MobileFinanceReceiptReference> Receipts) { public string Currency => "INR"; }
[System.Text.Json.Serialization.JsonDerivedType(typeof(MobileConcessionRequest))]
[System.Text.Json.Serialization.JsonDerivedType(typeof(MobileRefundRequest))]
[System.Text.Json.Serialization.JsonDerivedType(typeof(MobileAdjustmentRequest))]
public abstract record MobileFinanceRequest(Guid Id, string Kind)
{
 [System.Text.Json.Serialization.JsonIgnore] public abstract DateTimeOffset SortDate { get; }
 public string Currency => "INR";
}
public sealed record MobileConcessionRequest(Guid Id, Guid StudentId, Guid AcademicYearId, string ConcessionTypeName,
 string ConcessionAmount, string RequestedConcessionAmount, string ApprovedConcessionAmount, DateTimeOffset CreatedAt,
 string Status, string StatusName, string ConcurrencyStamp, IReadOnlyList<string> AvailableActions, string? Remarks) : MobileFinanceRequest(Id,"concession")
{ [System.Text.Json.Serialization.JsonIgnore] public override DateTimeOffset SortDate => CreatedAt; }
public sealed record MobileRefundRequest(Guid Id, string VoucherNumber, string TotalAmount, string CalculatedAmount, string ApprovedAmount,
 string ProcessedAmount, string ReversedAmount, DateTimeOffset CreationTime, DateTime? SubmittedAt, string Status,
 IReadOnlyList<string> AvailableActions, string? Reason, Guid? OriginalReceiptId, string? OriginalReceiptNumber) : MobileFinanceRequest(Id,"refund")
{ [System.Text.Json.Serialization.JsonIgnore] public override DateTimeOffset SortDate => CreationTime; }
public sealed record MobileAdjustmentRequest(Guid Id, string Status, string RequestedAmount, DateTimeOffset CreationTime,
 string? Reason, IReadOnlyList<string> AvailableActions) : MobileFinanceRequest(Id,"adjustment")
{ [System.Text.Json.Serialization.JsonIgnore] public override DateTimeOffset SortDate => CreationTime; }
public record MobileFinancePage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize) { public string Currency => "INR"; }
public record MobileFinancePaymentSummary(Guid StudentId, Guid AcademicYearId, string PaidAmount, int SuccessfulPaymentCount, DateOnly? LatestPaymentDate);
public record MobileFinanceRequestSummary(Guid StudentId, Guid AcademicYearId, string Kind, string TotalAmount, int CompletedRequestCount);
public record MobileFinanceList<T>(IReadOnlyList<T> Items, int TotalCount)
{
 public string Currency => "INR";
 [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
 public MobileFinancePaymentSummary? PaymentSummary { get; init; }
}
public record MobileFinanceRequestPage(IReadOnlyList<MobileFinanceRequest> Items, int TotalCount, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? CurrentPage, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PageSize, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? TotalPages)
{
 public string Currency => "INR";
 [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
 public MobileFinanceRequestSummary? RequestSummary { get; init; }
}
