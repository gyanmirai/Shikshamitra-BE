using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
namespace ShikshaMitra.Finance.Mobile;

public sealed class MobileConcessionRequestContext
{
 public Guid AcademicYearId { get; set; }
 public Guid StudentId { get; set; }
 public string StudentName { get; set; } = string.Empty;
 public Guid? StudentFeeStructureId { get; set; }
 public string FeeStructureName { get; set; } = string.Empty;
 public bool AssignmentSelectionRequired { get; set; }
 public List<MobileConcessionAssignmentOption> Assignments { get; set; } = [];
 public List<string> Blockers { get; set; } = [];
 public List<MobileConcessionEligibleFeeHead> FeeHeads { get; set; } = [];
 public List<MobileConcessionEligibleDueFee> EligibleDueFees { get; set; } = [];
 public List<MobileConcessionPolicyOption> ConcessionPolicies { get; set; } = [];
 public List<MobileConcessionTypeOption> ConcessionTypes { get; set; } = [];
 public string Currency => "INR";
 public bool CanCreate { get; set; }
 public bool CanEdit { get; set; }
 public bool CanSubmit { get; set; }
 public bool CanUploadDocuments { get; set; }
}

public sealed class MobileConcessionAssignmentOption
{
 public Guid StudentFeeStructureId { get; set; }
 public Guid FeeStructureId { get; set; }
 public string FeeStructureName { get; set; } = string.Empty;
 public DateTime ChargeStartDate { get; set; }
 public DateTime ChargeEndDate { get; set; }
}

public sealed class MobileConcessionEligibleFeeHead
{
 public Guid FeeHeadId { get; set; }
 public string FeeHeadName { get; set; } = string.Empty;
 public string EligibleOutstanding { get; set; } = "0.00";
}

public sealed class MobileConcessionEligibleDueFee
{
 public Guid DueFeeId { get; set; }
 public Guid FeeHeadId { get; set; }
 public string FeeHeadName { get; set; } = string.Empty;
 public int Month { get; set; }
 public int Year { get; set; }
 public string InstallmentLabel { get; set; } = string.Empty;
 public string OriginalAmount { get; set; } = "0.00";
 public string PaidAmount { get; set; } = "0.00";
 public string ConcessionAmount { get; set; } = "0.00";
 public string AdjustmentCreditAmount { get; set; } = "0.00";
 public string OutstandingAmount { get; set; } = "0.00";
 public string EligibleConcessionAmount { get; set; } = "0.00";
 public string Status { get; set; } = string.Empty;
 public string ConcurrencyStamp { get; set; } = string.Empty;
}

public sealed class MobileConcessionPolicyOption
{
 public bool AutoCreateRefund { get; set; }
 public Guid ConcessionPolicyId { get; set; }
 public Guid ConcessionPolicyVersionId { get; set; }
 public string PolicyName { get; set; } = string.Empty;
 public string PolicyCode { get; set; } = string.Empty;
 public int Version { get; set; }
 public bool IsDefault { get; set; }
 public string ValueSource { get; set; } = string.Empty;
 public string CalculationMethod { get; set; } = string.Empty;
 public string? PercentageValue { get; set; }
 public string? FixedAmount { get; set; }
 public string? MaximumPercentage { get; set; }
 public string? MaximumFixedAmount { get; set; }
 public string ApplyOn { get; set; } = string.Empty;
 public string RoundingMethod { get; set; } = string.Empty;
 public List<Guid> ApplicableConcessionTypeIds { get; set; } = [];
 public string FeeHeadScope { get; set; } = string.Empty;
 public List<Guid> ApplicableFeeHeadIds { get; set; } = [];
}

public sealed class MobileConcessionTypeOption
{
 public ConcessionBenefitBlockerDto? BenefitBlocker { get; set; }
 public Guid ConcessionTypeId { get; set; }
 public string Name { get; set; } = string.Empty;
 public string? Description { get; set; }
 public List<MobileConcessionDocumentRequirement> Documents { get; set; } = [];
}

public sealed class MobileConcessionDocumentRequirement
{
 public Guid DocumentId { get; set; }
 public string DocumentName { get; set; } = string.Empty;
 public bool IsRequired { get; set; }
 public long MaxFileSizeBytes { get; set; }
 public List<string> AcceptedFormats { get; set; } = [];
 public string? Url { get; set; }
 public bool IsUploaded { get; set; }
}

public sealed class MobileConcessionRequestPreview
{
 public Guid StudentFeeStructureId { get; set; }
 public Guid ConcessionPolicyId { get; set; }
 public Guid ConcessionPolicyVersionId { get; set; }
 public string ConcessionPolicyName { get; set; } = string.Empty;
 public string ConcessionPolicyCode { get; set; } = string.Empty;
 public int ConcessionPolicyVersion { get; set; }
 public string ApplyOn { get; set; } = string.Empty;
 public string ValueSource { get; set; } = string.Empty;
 public string CalculationMethod { get; set; } = string.Empty;
 public string? MaximumPercentage { get; set; }
 public string? MaximumFixedAmount { get; set; }
 public string TotalEligibleBase { get; set; } = "0.00";
 public string TotalConcessionAmount { get; set; } = "0.00";
 public string NetPayable { get; set; } = "0.00";
 public List<MobileFeeHeadConcessionDetail> FeeHeadConcessions { get; set; } = [];
 public List<MobileConcessionEligibleDueFee> EligibleDueFees { get; set; } = [];
 public string PreviewFingerprint { get; set; } = string.Empty;
 public DateTime GeneratedAt { get; set; }
 public string Currency => "INR";
}

public sealed class MobileFeeHeadConcessionDetail
{
 public Guid FeeHeadId { get; set; }
 public string FeeHeadName { get; set; } = string.Empty;
 public string AnnualBaseAmount { get; set; } = "0.00";
 public int BasedOn { get; set; }
 public string Amount { get; set; } = "0.00";
 public string DiscountAmount { get; set; } = "0.00";
 public string NetAmount { get; set; } = "0.00";
}

public sealed class MobileConcessionRequestDetail
{
 public ConcessionBenefitBlockerDto? BenefitBlocker { get; set; }
 public Guid ConcessionRequestId { get; set; }
 public Guid AcademicYearId { get; set; }
 public Guid StudentId { get; set; }
 public string StudentName { get; set; } = string.Empty;
 public Guid ConcessionTypeId { get; set; }
 public Guid StudentFeeStructureId { get; set; }
 public Guid ConcessionPolicyId { get; set; }
 public Guid ConcessionPolicyVersionId { get; set; }
 public string ConcessionPolicyName { get; set; } = string.Empty;
 public string ConcessionPolicyCode { get; set; } = string.Empty;
 public int ConcessionPolicyVersion { get; set; }
 public string ConcessionTypeName { get; set; } = string.Empty;
 public string Status { get; set; } = string.Empty;
 public string StatusName { get; set; } = string.Empty;
 public string ConcurrencyStamp { get; set; } = string.Empty;
 public DateTime? SubmittedAt { get; set; }
 public string? CancellationReason { get; set; }
 public string TotalRequestedConcessionAmount { get; set; } = "0.00";
 public string TotalApprovedConcessionAmount { get; set; } = "0.00";
 public string TotalReversedConcessionAmount { get; set; } = "0.00";
 public string NetApprovedConcessionAmount { get; set; } = "0.00";
 public string? Remarks { get; set; }
 public List<MobileFeeHeadConcessionDetail> FeeHeadConcessions { get; set; } = [];
 public List<MobileDueFeeConcession> DueFeeConcessions { get; set; } = [];
 public List<MobileConcessionDocumentRequirement> RequiredDocuments { get; set; } = [];
 public List<MobileConcessionSubmittedDocument> SubmittedDocuments { get; set; } = [];
 public string DocumentSubmissionState { get; set; } = string.Empty;
 public List<Guid> MissingDocumentIds { get; set; } = [];
 public DateTime CreatedAt { get; set; }
 public List<string> AvailableActions { get; set; } = [];
 public string Currency => "INR";
 public string Kind => "concession";
}

public sealed class MobileDueFeeConcession
{
 public Guid DueFeeId { get; set; }
 public Guid FeeHeadId { get; set; }
 public string Amount { get; set; } = "0.00";
}

public sealed class MobileConcessionSubmittedDocument
{
 public Guid DocumentId { get; set; }
 public string DocumentName { get; set; } = string.Empty;
 public string Url { get; set; } = string.Empty;
 public int Status { get; set; }
}

public sealed class MobileRefundEligibility
{
 public Guid? StudentId { get; set; }
 public Guid? FinanceFeeAccountId { get; set; }
 public Guid AcademicYearId { get; set; }
 public string TotalPaid { get; set; } = "0.00";
 public string TotalRefunded { get; set; } = "0.00";
 public string TotalRefundable { get; set; } = "0.00";
 public Guid? StudentFeeStructureId { get; set; }
 public Guid? RefundPolicyId { get; set; }
 public Guid? RefundPolicyVersionId { get; set; }
 public string? RefundPolicyName { get; set; }
 public List<string> AllowedPayoutMethods { get; set; } = [];
 public int? ProcessingTargetDays { get; set; }
 public bool AllowPartialRefund { get; set; }
 public List<MobileRefundEligibleReceipt> Receipts { get; set; } = [];
 public List<MobileRefundEligibilityFeeHead> FeeHeads { get; set; } = [];
 public string Currency => "INR";
}

public sealed class MobileRefundEligibleReceipt
{
 public Guid ReceiptId { get; set; }
 public string? ChargeName { get; set; }
 public string ReceiptNumber { get; set; } = string.Empty;
 public DateOnly ReceiptDate { get; set; }
 public string PaidAmount { get; set; } = "0.00";
 public string RefundableAmount { get; set; } = "0.00";
}

public sealed class MobileRefundEligibilityFeeHead
{
 public Guid? FeeHeadId { get; set; }
 public string FeeHeadName { get; set; } = string.Empty;
 public string TotalPaid { get; set; } = "0.00";
 public string TotalRefunded { get; set; } = "0.00";
 public string TotalRefundable { get; set; } = "0.00";
 public List<MobileRefundEligibilityMonth> Months { get; set; } = [];
}

public sealed class MobileRefundEligibilityMonth
{
 public Guid? ChargeId { get; set; }
 public Guid? LateFeeDueFeeId { get; set; }
 public Guid? DueFeeId { get; set; }
 public Guid SourceAllocationId { get; set; }
 public Guid SourceReceiptId { get; set; }
 public string? ChargeName { get; set; }
 public string ReceiptNumber { get; set; } = string.Empty;
 public MonthEnum Month { get; set; }
 public int Year { get; set; }
 public string Total { get; set; } = "0.00";
 public string Paid { get; set; } = "0.00";
 public string Refunded { get; set; } = "0.00";
 public string Refundable { get; set; } = "0.00";
}

public sealed class MobileFeeRefundPreview
{
 public Guid? StudentFeeStructureId { get; set; }
 public Guid RefundPolicyId { get; set; }
 public Guid RefundPolicyVersionId { get; set; }
 public string RefundPolicyName { get; set; } = string.Empty;
 public string SettledPaidAmount { get; set; } = "0.00";
 public string PriorProcessedRefund { get; set; } = "0.00";
 public string ActiveReservation { get; set; } = "0.00";
 public string RequestedAmount { get; set; } = "0.00";
 public string DeductionAmount { get; set; } = "0.00";
 public string CapAdjustment { get; set; } = "0.00";
 public string MaximumAllowedAmount { get; set; } = "0.00";
 public bool IsAllowed { get; set; }
 public List<string> Reasons { get; set; } = [];
 public string Currency => "INR";
 public string PreviewFingerprint { get; set; } = string.Empty;
}
