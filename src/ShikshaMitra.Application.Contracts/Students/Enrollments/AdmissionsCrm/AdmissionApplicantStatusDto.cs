using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Students.StudentEnrollments;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionApplicantStatusDto
{
	public Guid LeadId { get; set; }
	public string Language { get; set; } = "en";
	public IReadOnlyList<string> SupportedLanguages { get; set; } = ["en", "gu", "hi"];
	public string DisplayName { get; set; } = string.Empty;
	public AdmissionInquiryStage Stage { get; set; }
	public string StageName { get; set; } = string.Empty;
	public DateTimeOffset LastActivityTime { get; set; }
	public string NextStep { get; set; } = string.Empty;
	public List<AdmissionApplicantStatusChecklistItemDto> Checklist { get; set; } = new();
	public List<AdmissionApplicantStatusChecklistItemDto> ApplicantChecklist { get; set; } = new();
	public List<AdmissionApplicantStatusActivityDto> Activity { get; set; } = new();
	public AdmissionApplicantStatusExamDto? Exam { get; set; }
	public string? AccessSessionToken { get; set; }
	public DateTime? AccessSessionExpiresAt { get; set; }
	public AdmissionCrmRegistrationApplicationDto? Registration { get; set; }
	public AdmissionCrmApplicantDocumentChecklistDto? Documents { get; set; }
	public AdmissionApplicantStatusOfferDto? Offer { get; set; }
	public AdmissionApplicantFinancialStatusDto? FinancialStatus { get; set; }
	public AdmissionApplicantRouteStatusDto? AdmissionRoute { get; set; }
	public AdmissionApplicantActivationStatusDto? Activation { get; set; }
	public List<AdmissionFamilyApplicationSummaryDto> FamilyApplications { get; set; } = new();
	public PublicAdmissionCommunicationPreferencesDto CommunicationPreferences { get; set; } = new();
	public bool CanWithdrawApplication { get; set; }
}

public class PublicAdmissionCommunicationPreferencesDto
{
	public bool WhatsApp { get; set; }
	public bool Sms { get; set; }
	public bool Email { get; set; }
	public bool HasPhoneContact { get; set; }
	public bool HasEmailContact { get; set; }
}

public class UpdatePublicAdmissionCommunicationPreferencesDto
{
	[Required]
	public string AccessSessionToken { get; set; } = string.Empty;
	public bool WhatsApp { get; set; }
	public bool Sms { get; set; }
	public bool Email { get; set; }
}

public class WithdrawPublicAdmissionApplicationDto
{
	[Required]
	public string AccessSessionToken { get; set; } = string.Empty;

	[Required]
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string Reason { get; set; } = string.Empty;
}

public class AdmissionFamilyApplicationSummaryDto
{
	public Guid LeadId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string StatusToken { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string? DesiredClassName { get; set; }
	public AdmissionInquiryStage Stage { get; set; }
	public string StageName { get; set; } = string.Empty;
	public DateTimeOffset LastActivityTime { get; set; }
}

public class AdmissionApplicantRouteStatusDto
{
	public AdmissionCrmRouteType RouteType { get; set; }
	public AdmissionCrmRouteEligibilityStatus EligibilityStatus { get; set; }
	public DateTime AssignedAt { get; set; }
	public DateTime? EvaluatedAt { get; set; }
}

public class AdmissionApplicantActivationStatusDto
{
	public string ActivationReference { get; set; } = string.Empty;
	public AdmissionCrmActivationStatus Status { get; set; }
	public DateTime? FirstDayReadyAt { get; set; }
}

public class AdmissionApplicantFinancialStatusDto
{
	public Guid QuotationId { get; set; }
	public string QuotationNumber { get; set; } = string.Empty;
	public string Currency { get; set; } = "INR";
	public IReadOnlyList<AdmissionApplicantFeeComponentDto> Components { get; set; } = [];
	public decimal GrossAmount { get; set; }
	public decimal ConcessionAmount { get; set; }
	public decimal NetAmount { get; set; }
	public decimal ConfirmedAmount { get; set; }
	public decimal OutstandingAmount { get; set; }
	public AdmissionCrmFinanceState FinanceState { get; set; }
	public AdmissionCrmPaymentGateType GateType { get; set; }
	public AdmissionCrmPaymentGateMilestone GateMilestone { get; set; }
	public bool IsGateSatisfied { get; set; }
	public DateTime ValidUntil { get; set; }
	public string? FinanceReference { get; set; }
}

public class AdmissionApplicantFeeComponentDto
{
	public string Name { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public bool IsRefundable { get; set; }
	public string? ParentDescription { get; set; }
}

public class AdmissionApplicantStatusOfferDto
{
	public Guid OfferId { get; set; }
	public string OfferNumber { get; set; } = string.Empty;
	public AdmissionCrmSeatOfferType OfferType { get; set; }
	public AdmissionCrmSeatOfferStatus Status { get; set; }
	public IReadOnlyDictionary<string, string?> Terms { get; set; } = new Dictionary<string, string?>();
	public string Language { get; set; } = string.Empty;
	public DateTime ValidFrom { get; set; }
	public DateTime ValidUntil { get; set; }
	public DateTime? OpenedAt { get; set; }
	public DateTime? RespondedAt { get; set; }
	public string? ResponseReason { get; set; }
}

public class RespondAdmissionApplicantOfferDto
{
	public required string AccessSessionToken { get; set; }
	public bool Accept { get; set; }
	public string? Reason { get; set; }
	public string Language { get; set; } = "en";
	public IReadOnlyDictionary<string, bool> Declarations { get; set; } = new Dictionary<string, bool>();
}

public class AdmissionApplicantStatusActivityDto
{
	public string Type { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string? Details { get; set; }
	public DateTimeOffset OccurredAt { get; set; }
}

public class AdmissionApplicantStatusChecklistItemDto
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string? StatusCode { get; set; }
	public string? Details { get; set; }
	public bool IsRequired { get; set; }
	public bool IsComplete { get; set; }
	public DateTimeOffset? StatusChangedAt { get; set; }
}

public class AdmissionApplicantStatusExamDto
{
	public bool Scheduled { get; set; }
	public string? SlotName { get; set; }
	public DateTimeOffset? ExamStartTime { get; set; }
	public DateTimeOffset? ExamEndTime { get; set; }
	public int? RoomNo { get; set; }
	public AdmissionCrmExamAttendanceStatus? AttendanceStatus { get; set; }
	public EntranceExamStatusEnum? ExamStatus { get; set; }
	public double? TotalMarks { get; set; }
	public double? PassingMarks { get; set; }
	public double? ObtainedMarks { get; set; }
	public double? Percentage { get; set; }
	public int? MeritRank { get; set; }
	public AdmissionCrmEligibilityDecision? EligibilityDecision { get; set; }
	public string? EligibilityDecisionReason { get; set; }
}
