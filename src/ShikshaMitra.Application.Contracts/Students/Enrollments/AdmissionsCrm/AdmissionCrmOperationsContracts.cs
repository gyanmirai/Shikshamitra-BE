using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOperationsQueueSummaryDto
{
	public Guid AcademicYearId { get; set; }
	public DateTime GeneratedAt { get; set; }
	public IReadOnlyList<AdmissionCrmOperationsQueueCountDto> Queues { get; set; } = [];
}

public class AdmissionCrmOperationsQueueCountDto
{
	public AdmissionCrmOperationsQueueType QueueType { get; set; }
	public string Label { get; set; } = string.Empty;
	public int Count { get; set; }
	public int UrgentCount { get; set; }
}

public class AdmissionCrmOperationsQueueDto
{
	public Guid AcademicYearId { get; set; }
	public AdmissionCrmOperationsQueueType QueueType { get; set; }
	public string Label { get; set; } = string.Empty;
	public DateTime GeneratedAt { get; set; }
	public int TotalCount { get; set; }
	public IReadOnlyList<AdmissionCrmOperationsQueueItemDto> Items { get; set; } = [];
}

public class GetAdmissionCrmOperationsQueueInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public string? ApplicantSearch { get; set; }
	public string? ActionSearch { get; set; }
	public Guid? OwnerUserId { get; set; }
	public Guid? AssignedToUserId { get; set; }
	public bool? UrgentOnly { get; set; }
	public int? MinimumAgeDays { get; set; }
	public DateTimeOffset? DueBefore { get; set; }
	public AdmissionCrmRegistrationStatus? RegistrationStatus { get; set; }
	public AdmissionCrmApplicantDocumentStatus? DocumentStatus { get; set; }
	public AdmissionCrmSeatOfferStatus? OfferStatus { get; set; }
	public AdmissionCrmRouteType? RouteType { get; set; }
	public Guid? SeatPoolId { get; set; }
	public AdmissionCrmActivationStatus? ActivationStatus { get; set; }
	public AdmissionCrmServiceHandoffStatus? ServiceHandoffStatus { get; set; }
	public DateTime? RegistrationLinkExpiresFrom { get; set; }
	public DateTime? RegistrationLinkExpiresUntil { get; set; }
	public DateTime? OfferExpiresFrom { get; set; }
	public DateTime? OfferExpiresUntil { get; set; }
	public bool? PaymentGateSatisfied { get; set; }
	public bool? PlacementConfigured { get; set; }
	public bool? AccountProvisioningPending { get; set; }
	public string? RepairSearch { get; set; }
}

public class CreateAdmissionCrmOperationsSavedViewDto
{
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmOperationsQueueType QueueType { get; set; }
	public GetAdmissionCrmOperationsQueueInput Filters { get; set; } = new();
	public bool IsShared { get; set; }
}

public class AdmissionCrmOperationsSavedViewDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid OwnerUserId { get; set; }
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmOperationsQueueType QueueType { get; set; }
	public GetAdmissionCrmOperationsQueueInput Filters { get; set; } = new();
	public bool IsShared { get; set; }
}

public class AdmissionCrmOperationsQueueItemDto
{
	public string Key { get; set; } = string.Empty;
	public Guid LeadId { get; set; }
	public int ApplicantNumber { get; set; }
	public Guid? ExamRecordId { get; set; }
	public Guid? RegistrationApplicationId { get; set; }
	public Guid? DocumentId { get; set; }
	public Guid? ChecklistAssignmentId { get; set; }
	public Guid? ServiceHandoffId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string? Details { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTimeOffset? DueAt { get; set; }
	public int AgeDays { get; set; }
	public bool IsUrgent { get; set; }
	public Guid? OwnerUserId { get; set; }
	public AdmissionCrmRegistrationQueueContextDto? Registration { get; set; }
	public AdmissionCrmOfferQueueContextDto? Offer { get; set; }
	public AdmissionCrmActivationQueueContextDto? Activation { get; set; }
	public AdmissionCrmOperationQueueContextDto? Operation { get; set; }
}

public class AdmissionCrmRegistrationQueueContextDto
{
	public Guid? RegistrationApplicationId { get; set; }
	public AdmissionCrmRegistrationStatus? Status { get; set; }
	public string? FormNumber { get; set; }
	public AdmissionCrmRegistrationProvenance? Provenance { get; set; }
	public AdmissionCrmRegistrationDataEntryState? DataEntryState { get; set; }
	public string? PhysicalFormNumber { get; set; }
	public DateTime? PhysicalFormReceivedDate { get; set; }
	public decimal CompletionPercentage { get; set; }
	public DateTime? LinkExpiresAt { get; set; }
	public AdmissionCrmRegistrationFeeStatus? FeeStatus { get; set; }
	public int DocumentTotalCount { get; set; }
	public int DocumentResolvedCount { get; set; }
	public int DocumentPendingCount { get; set; }
	public AdmissionCrmApplicantDocumentStatus? CurrentDocumentStatus { get; set; }
	public bool ReadinessConfirmed { get; set; }
}

public class AdmissionCrmOfferQueueContextDto
{
	public Guid? OfferId { get; set; }
	public string? OfferNumber { get; set; }
	public AdmissionCrmSeatOfferStatus? OfferStatus { get; set; }
	public DateTime? OfferExpiresAt { get; set; }
	public Guid? SeatPoolId { get; set; }
	public string? SeatPoolName { get; set; }
	public int? RemainingSeats { get; set; }
	public AdmissionCrmAdmissionDecisionStatus? DecisionStatus { get; set; }
	public AdmissionCrmRouteType? RouteType { get; set; }
	public AdmissionCrmRouteEligibilityStatus? RouteStatus { get; set; }
	public AdmissionCrmFinanceState? FinanceState { get; set; }
	public bool? PaymentGateSatisfied { get; set; }
}

public class AdmissionCrmActivationQueueContextDto
{
	public Guid? ActivationId { get; set; }
	public Guid? StudentId { get; set; }
	public string? ActivationReference { get; set; }
	public AdmissionCrmActivationStatus? Status { get; set; }
	public bool PlacementConfigured { get; set; }
	public int? RollNumber { get; set; }
	public bool RequireStudentAccount { get; set; }
	public bool RequireGuardianAccount { get; set; }
	public int AttemptCount { get; set; }
	public string? RepairStep { get; set; }
	public string? RepairReason { get; set; }
}

public class AdmissionCrmOperationQueueContextDto
{
	public AdmissionCrmOperationsQueueType QueueType { get; set; }
	public string Severity { get; set; } = "Normal";
	public string AssignmentStatus { get; set; } = "Unassigned";
	public AdmissionCrmServiceHandoffStatus? ServiceHandoffStatus { get; set; }
	public int? RetryAttemptCount { get; set; }
}

public class AdmissionCrmEnrollmentOverviewDto
{
	public Guid AcademicYearId { get; set; }
	public DateTime GeneratedAt { get; set; }
	public int UnassignedLeadCount { get; set; }
	public int UpcomingExamCount { get; set; }
	public int ExamNoShowCount { get; set; }
	public int PendingPaperEntryCount { get; set; }
	public int ExpiringRegistrationCount { get; set; }
	public int DocumentFollowUpCount { get; set; }
	public int FeeFollowUpCount { get; set; }
	public int PendingApprovalCount { get; set; }
	public int ExpiringOfferCount { get; set; }
	public int ClassPlacementRiskCount { get; set; }
	public int ActivationFailureCount { get; set; }
	public int HandoffBlockerCount { get; set; }
	public int UrgentCount { get; set; }
	public IReadOnlyList<AdmissionCrmOperationsQueueCountDto> Queues { get; set; } = [];
	public IReadOnlyList<AdmissionCrmEnrollmentOverviewItemDto> UrgentItems { get; set; } = [];
}

public class AdmissionCrmEnrollmentOverviewItemDto
{
	public string Workspace { get; set; } = string.Empty;
	public AdmissionCrmOperationsQueueType QueueType { get; set; }
	public AdmissionCrmOperationsQueueItemDto Item { get; set; } = new();
}

public enum AdmissionCrmMyWorkType
{
	Task = 0,
	OperationalBlocker = 1
}

public enum AdmissionCrmWorkUrgency
{
	Normal = 0,
	DueSoon = 1,
	DueToday = 2,
	Overdue = 3
}

public enum AdmissionCrmRecommendedAction
{
	OpenApplicant = 0,
	CompleteTask = 1,
	ScheduleExam = 2,
	RecordAttendance = 3,
	RescheduleExam = 4,
	RecordResult = 5,
	DecideEligibility = 6,
	ReviewRegistration = 7,
	ReviewDocuments = 8,
	ReviewOffer = 9,
	RepairActivation = 10
}

public enum AdmissionCrmWorkspaceSection
{
	Overview = 0,
	EntranceExam = 1,
	Registration = 2,
	Offer = 3,
	Activation = 4,
	Governance = 5
}

public class GetAdmissionCrmMyWorkInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public string? ApplicantSearch { get; set; }
	public string? TargetClassSearch { get; set; }
	public string? AssignedSearch { get; set; }
	public AdmissionCrmMyWorkType? WorkType { get; set; }
	public AdmissionCrmOperationsQueueType? QueueType { get; set; }
	public AdmissionInquiryStage? Stage { get; set; }
	public Guid? TargetStandardId { get; set; }
	public Guid? AcademicMediumId { get; set; }
	public AdmissionCrmLeadPriority? Priority { get; set; }
	public AdmissionCrmWorkUrgency? Urgency { get; set; }
}

public class AdmissionCrmMyWorkDto
{
	public Guid AcademicYearId { get; set; }
	public DateTime GeneratedAt { get; set; }
	public int AssignedTotal { get; set; }
	public int OverdueCount { get; set; }
	public int DueTodayCount { get; set; }
	public int UrgentBlockerCount { get; set; }
	public long TotalCount { get; set; }
	public IReadOnlyList<AdmissionCrmMyWorkItemDto> Items { get; set; } = [];
}

public class AdmissionCrmMyWorkItemDto
{
	public string Key { get; set; } = string.Empty;
	public AdmissionCrmMyWorkType WorkType { get; set; }
	public Guid? TaskId { get; set; }
	public AdmissionCrmOperationsQueueType? QueueType { get; set; }
	public Guid LeadId { get; set; }
	public int ApplicantNumber { get; set; }
	public Guid? ExamRecordId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string? Contact { get; set; }
	public Guid? TargetStandardId { get; set; }
	public string? TargetClassName { get; set; }
	public AdmissionInquiryStage Stage { get; set; }
	public AdmissionCrmPipelineColumnKey PipelineColumn { get; set; }
	public AdmissionCrmApplicantStepKey CurrentStep { get; set; }
	public AdmissionCrmLeadPriority Priority { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Details { get; set; }
	public string NextAction { get; set; } = string.Empty;
	public string? NextActionCode { get; set; }
	public DateTimeOffset? DueAt { get; set; }
	public AdmissionCrmWorkUrgency Urgency { get; set; }
	public bool IsUrgent { get; set; }
	public Guid? OwnerUserId { get; set; }
	public string? OwnerDisplayName { get; set; }
	public Guid? AssignedUserId { get; set; }
	public string? AssignedDisplayName { get; set; }
	public AdmissionCrmRecommendedAction RecommendedAction { get; set; }
	public AdmissionCrmWorkspaceSection RecommendedSection { get; set; }
	public IReadOnlyList<AdmissionCrmRecommendedAction> AllowedActions { get; set; } = [];
}

public class AdmissionCrmAdmissionHandoffPackageDto
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid RegistrationApplicationId { get; set; }
	public Guid ChecklistAssignmentId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public string FormNumber { get; set; } = string.Empty;
	public AdmissionInquiryStage LeadStage { get; set; }
	public AdmissionCrmRegistrationStatus RegistrationStatus { get; set; }
	public DateTime RegistrationVerifiedAt { get; set; }
	public DateTime ReadinessConfirmedAt { get; set; }
	public bool ReadinessOverridden { get; set; }
	public string? ReadinessReason { get; set; }
	public AdmissionCrmEligibilityDecision? EligibilityDecision { get; set; }
	public int? MeritRank { get; set; }
	public string? PaymentReference { get; set; }
	public AdmissionCrmRegistrationFeeStatus FeeStatus { get; set; }
	public IReadOnlyDictionary<string, string?> ApplicantProfile { get; set; } = new Dictionary<string, string?>();
	public IReadOnlyList<AdmissionCrmAdmissionHandoffDocumentDto> Documents { get; set; } = [];
	public DateTime GeneratedAt { get; set; }
}

public class AdmissionCrmAdmissionHandoffDocumentDto
{
	public string RequirementKey { get; set; } = string.Empty;
	public int Version { get; set; }
	public AdmissionCrmApplicantDocumentStatus Status { get; set; }
	public string? FileName { get; set; }
	public DateTime? ValidUntil { get; set; }
	public DateTime? DecisionAt { get; set; }
	public DateTime? OriginalSeenAt { get; set; }
}
