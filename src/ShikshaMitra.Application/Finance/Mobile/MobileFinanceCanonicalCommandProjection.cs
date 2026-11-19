using System.Linq;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
namespace ShikshaMitra.Finance.Mobile;

public static class MobileFinanceCanonicalCommandProjection
{
 public static MobileConcessionRequestContext Map(ConcessionRequestContextDto x) => new()
 {
  AcademicYearId = x.AcademicYearId,
  StudentId = x.StudentId,
  StudentName = x.StudentName,
  StudentFeeStructureId = x.StudentFeeStructureId,
  FeeStructureName = x.FeeStructureName,
  AssignmentSelectionRequired = x.AssignmentSelectionRequired,
  Assignments = x.Assignments.Select(Map).ToList(),
  Blockers = x.Blockers,
  FeeHeads = x.FeeHeads.Select(Map).ToList(),
  EligibleDueFees = x.EligibleDueFees.Select(Map).ToList(),
  ConcessionPolicies = x.ConcessionPolicies.Select(Map).ToList(),
  ConcessionTypes = x.ConcessionTypes.Select(Map).ToList(),
 };
 public static MobileConcessionAssignmentOption Map(ConcessionAssignmentOptionDto x) => new()
 {
  StudentFeeStructureId = x.StudentFeeStructureId,
  FeeStructureId = x.FeeStructureId,
  FeeStructureName = x.FeeStructureName,
  ChargeStartDate = x.ChargeStartDate,
  ChargeEndDate = x.ChargeEndDate,
 };
 public static MobileConcessionEligibleFeeHead Map(ConcessionEligibleFeeHeadDto x) => new()
 {
  FeeHeadId = x.FeeHeadId,
  FeeHeadName = x.FeeHeadName,
  EligibleOutstanding = MobileFinanceProjection.Money(x.EligibleOutstanding),
 };
 public static MobileConcessionEligibleDueFee Map(ConcessionEligibleDueFeeDto x) => new()
 {
  DueFeeId = x.DueFeeId,
  FeeHeadId = x.FeeHeadId,
  FeeHeadName = x.FeeHeadName,
  Month = x.Month,
  Year = x.Year,
  InstallmentLabel = x.InstallmentLabel,
  OriginalAmount = MobileFinanceProjection.Money(x.OriginalAmount),
  PaidAmount = MobileFinanceProjection.Money(x.PaidAmount),
  ConcessionAmount = MobileFinanceProjection.Money(x.ConcessionAmount),
  AdjustmentCreditAmount = MobileFinanceProjection.Money(x.AdjustmentCreditAmount),
  OutstandingAmount = MobileFinanceProjection.Money(x.OutstandingAmount),
  EligibleConcessionAmount = MobileFinanceProjection.Money(x.EligibleConcessionAmount),
  Status = x.Status,
  ConcurrencyStamp = x.ConcurrencyStamp,
 };
 public static MobileConcessionPolicyOption Map(ConcessionPolicyOptionDto x) => new()
 {
  AutoCreateRefund = x.AutoCreateRefund,
  ConcessionPolicyId = x.ConcessionPolicyId,
  ConcessionPolicyVersionId = x.ConcessionPolicyVersionId,
  PolicyName = x.PolicyName,
  PolicyCode = x.PolicyCode,
  Version = x.Version,
  IsDefault = x.IsDefault,
  ValueSource = x.ValueSource,
  CalculationMethod = x.CalculationMethod,
  PercentageValue = x.PercentageValue.HasValue ? MobileFinanceProjection.Money(x.PercentageValue.Value) : null,
  FixedAmount = x.FixedAmount.HasValue ? MobileFinanceProjection.Money(x.FixedAmount.Value) : null,
  MaximumPercentage = x.MaximumPercentage.HasValue ? MobileFinanceProjection.Money(x.MaximumPercentage.Value) : null,
  MaximumFixedAmount = x.MaximumFixedAmount.HasValue ? MobileFinanceProjection.Money(x.MaximumFixedAmount.Value) : null,
  ApplyOn = x.ApplyOn,
  RoundingMethod = x.RoundingMethod,
  ApplicableConcessionTypeIds = x.ApplicableConcessionTypeIds,
  FeeHeadScope = x.FeeHeadScope,
  ApplicableFeeHeadIds = x.ApplicableFeeHeadIds,
 };
 public static MobileConcessionTypeOption Map(ConcessionTypeOptionDto x) => new()
 {
  BenefitBlocker = x.BenefitBlocker,
  ConcessionTypeId = x.ConcessionTypeId,
  Name = x.Name,
  Description = x.Description,
  Documents = x.Documents.Select(Map).ToList(),
 };
 public static MobileConcessionDocumentRequirement Map(ConcessionDocumentRequirementDto x) => new()
 {
  DocumentId = x.DocumentId,
  DocumentName = x.DocumentName,
  IsRequired = x.IsRequired,
  MaxFileSizeBytes = x.MaxFileSizeBytes,
  AcceptedFormats = x.AcceptedFormats.Select(value => value.ToString()).ToList(),
  Url = x.Url,
  IsUploaded = x.IsUploaded,
 };
 public static MobileConcessionRequestPreview Map(ConcessionRequestPreviewDto x) => new()
 {
  StudentFeeStructureId = x.StudentFeeStructureId,
  ConcessionPolicyId = x.ConcessionPolicyId,
  ConcessionPolicyVersionId = x.ConcessionPolicyVersionId,
  ConcessionPolicyName = x.ConcessionPolicyName,
  ConcessionPolicyCode = x.ConcessionPolicyCode,
  ConcessionPolicyVersion = x.ConcessionPolicyVersion,
  ApplyOn = x.ApplyOn,
  ValueSource = x.ValueSource,
  CalculationMethod = x.CalculationMethod,
  MaximumPercentage = x.MaximumPercentage.HasValue ? MobileFinanceProjection.Money(x.MaximumPercentage.Value) : null,
  MaximumFixedAmount = x.MaximumFixedAmount.HasValue ? MobileFinanceProjection.Money(x.MaximumFixedAmount.Value) : null,
  TotalEligibleBase = MobileFinanceProjection.Money(x.TotalEligibleBase),
  TotalConcessionAmount = MobileFinanceProjection.Money(x.TotalConcessionAmount),
  NetPayable = MobileFinanceProjection.Money(x.NetPayable),
  FeeHeadConcessions = x.FeeHeadConcessions.Select(Map).ToList(),
  EligibleDueFees = x.EligibleDueFees.Select(Map).ToList(),
  PreviewFingerprint = x.PreviewFingerprint,
  GeneratedAt = x.GeneratedAt,
 };
 public static MobileFeeHeadConcessionDetail Map(FeeHeadConcessionDetailDto x) => new()
 {
  FeeHeadId = x.FeeHeadId,
  FeeHeadName = x.FeeHeadName,
  AnnualBaseAmount = MobileFinanceProjection.Money(x.AnnualBaseAmount),
  BasedOn = x.BasedOn,
  Amount = MobileFinanceProjection.Money(x.Amount),
  DiscountAmount = MobileFinanceProjection.Money(x.DiscountAmount),
  NetAmount = MobileFinanceProjection.Money(x.NetAmount),
 };
 public static MobileConcessionRequestDetail Map(ConcessionRequestDetailDto x) => new()
 {
  ConcessionRequestId = x.ConcessionRequestId,
  AcademicYearId = x.AcademicYearId,
  StudentId = x.StudentId,
  StudentName = x.StudentName,
  ConcessionTypeId = x.ConcessionTypeId,
  StudentFeeStructureId = x.StudentFeeStructureId,
  ConcessionPolicyId = x.ConcessionPolicyId,
  ConcessionPolicyVersionId = x.ConcessionPolicyVersionId,
  ConcessionPolicyName = x.ConcessionPolicyName,
  ConcessionPolicyCode = x.ConcessionPolicyCode,
  ConcessionPolicyVersion = x.ConcessionPolicyVersion,
  ConcessionTypeName = x.ConcessionTypeName,
  Status = x.Status.ToString(),
  StatusName = x.StatusName,
  ConcurrencyStamp = x.ConcurrencyStamp,
  SubmittedAt = x.SubmittedAt,
  CancellationReason = x.CancellationReason,
  TotalRequestedConcessionAmount = MobileFinanceProjection.Money(x.TotalRequestedConcessionAmount),
  TotalApprovedConcessionAmount = MobileFinanceProjection.Money(x.TotalApprovedConcessionAmount),
  TotalReversedConcessionAmount = MobileFinanceProjection.Money(x.TotalReversedConcessionAmount),
  NetApprovedConcessionAmount = MobileFinanceProjection.Money(x.NetApprovedConcessionAmount),
  Remarks = x.Remarks,
  FeeHeadConcessions = x.FeeHeadConcessions.Select(Map).ToList(),
  DueFeeConcessions = x.DueFeeConcessions.Select(Map).ToList(),
  RequiredDocuments = x.RequiredDocuments.Select(Map).ToList(),
  SubmittedDocuments = x.SubmittedDocuments.Select(Map).ToList(),
  DocumentSubmissionState = x.DocumentSubmissionState,
  MissingDocumentIds = x.MissingDocumentIds,
  CreatedAt = x.CreatedAt,
  AvailableActions = x.AvailableActions,
 };
 public static MobileDueFeeConcession Map(DueFeeConcessionDto x) => new()
 {
  DueFeeId = x.DueFeeId,
  FeeHeadId = x.FeeHeadId,
  Amount = MobileFinanceProjection.Money(x.Amount),
 };
 public static MobileConcessionSubmittedDocument Map(ConcessionSubmittedDocumentDto x) => new()
 {
  DocumentId = x.DocumentId,
  DocumentName = x.DocumentName,
  Url = x.Url,
  Status = x.Status,
 };
 public static MobileRefundEligibility Map(RefundEligibilityDto x) => new()
 {
  StudentId = x.StudentId,
  FinanceFeeAccountId = x.FinanceFeeAccountId,
  AcademicYearId = x.AcademicYearId,
  TotalPaid = MobileFinanceProjection.Money(x.TotalPaid),
  TotalRefunded = MobileFinanceProjection.Money(x.TotalRefunded),
  TotalRefundable = MobileFinanceProjection.Money(x.TotalRefundable),
  StudentFeeStructureId = x.StudentFeeStructureId,
  RefundPolicyId = x.RefundPolicyId,
  RefundPolicyVersionId = x.RefundPolicyVersionId,
  RefundPolicyName = x.RefundPolicyName,
  AllowedPayoutMethods = x.AllowedPayoutMethods,
  ProcessingTargetDays = x.ProcessingTargetDays,
  AllowPartialRefund = x.AllowPartialRefund,
  Receipts = x.Receipts.Select(Map).ToList(),
  FeeHeads = x.FeeHeads.Select(Map).ToList(),
 };
 public static MobileRefundEligibleReceipt Map(RefundEligibleReceiptDto x) => new()
 {
  ReceiptId = x.ReceiptId,
  ChargeName = x.ChargeName,
  ReceiptNumber = x.ReceiptNumber,
  ReceiptDate = x.ReceiptDate,
  PaidAmount = MobileFinanceProjection.Money(x.PaidAmount),
  RefundableAmount = MobileFinanceProjection.Money(x.RefundableAmount),
 };
 public static MobileRefundEligibilityFeeHead Map(RefundEligibilityFeeHeadDto x) => new()
 {
  FeeHeadId = x.FeeHeadId,
  FeeHeadName = x.FeeHeadName,
  TotalPaid = MobileFinanceProjection.Money(x.TotalPaid),
  TotalRefunded = MobileFinanceProjection.Money(x.TotalRefunded),
  TotalRefundable = MobileFinanceProjection.Money(x.TotalRefundable),
  Months = x.Months.Select(Map).ToList(),
 };
 public static MobileRefundEligibilityMonth Map(RefundEligibilityMonthDto x) => new()
 {
  ChargeId = x.ChargeId,
  LateFeeDueFeeId = x.LateFeeDueFeeId,
  DueFeeId = x.DueFeeId,
  SourceAllocationId = x.SourceAllocationId,
  SourceReceiptId = x.SourceReceiptId,
  ChargeName = x.ChargeName,
  ReceiptNumber = x.ReceiptNumber,
  Month = x.Month,
  Year = x.Year,
  Total = MobileFinanceProjection.Money(x.Total),
  Paid = MobileFinanceProjection.Money(x.Paid),
  Refunded = MobileFinanceProjection.Money(x.Refunded),
  Refundable = MobileFinanceProjection.Money(x.Refundable),
 };
 public static MobileFeeRefundPreview Map(FeeRefundPreviewDto x) => new()
 {
  StudentFeeStructureId = x.StudentFeeStructureId,
  RefundPolicyId = x.RefundPolicyId,
  RefundPolicyVersionId = x.RefundPolicyVersionId,
  RefundPolicyName = x.RefundPolicyName,
  SettledPaidAmount = MobileFinanceProjection.Money(x.SettledPaidAmount),
  PriorProcessedRefund = MobileFinanceProjection.Money(x.PriorProcessedRefund),
  ActiveReservation = MobileFinanceProjection.Money(x.ActiveReservation),
  RequestedAmount = MobileFinanceProjection.Money(x.RequestedAmount),
  DeductionAmount = MobileFinanceProjection.Money(x.DeductionAmount),
  CapAdjustment = MobileFinanceProjection.Money(x.CapAdjustment),
  MaximumAllowedAmount = MobileFinanceProjection.Money(x.MaximumAllowedAmount),
  IsAllowed = x.IsAllowed,
  Reasons = x.Reasons,
 };
}
