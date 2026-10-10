using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationListInput : PagedAndSortedResultRequestDto
{
	/// <summary>Restricts the decision/finalization menu without limiting applicant history queries.</summary>
	public bool DecisionWorkspaceOnly { get; set; }
	public string? Search { get; set; }
	public AdmissionApplicationStatus? Status { get; set; }
	public Guid? AcademicYearId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StandardId { get; set; }
	public List<Guid> StandardIds { get; set; } = [];
	public Guid? AdmissionCycleId { get; set; }
	public Guid? AdmissionOfferingId { get; set; }
	public Guid? AssignedCounsellorId { get; set; }
	public Guid? CurrentStageInstanceId { get; set; }
	public string? CurrentStageSearch { get; set; }
}
public partial class AdmissionApplicationDto : FullAuditedEntityDto<Guid> { public string? ConcurrencyStamp { get; set; } public string ApplicationNumber { get; set; } = string.Empty; public Guid ApplicantId { get; set; } public string ApplicantName { get; set; } = string.Empty; public Guid AdmissionCycleId { get; set; } public Guid AdmissionOfferingId { get; set; } public Guid? AssignedCounsellorId { get; set; } public string? AssignedCounsellorName { get; set; } public string OfferingCode { get; set; } = string.Empty; public string CurrentStageName { get; set; } = string.Empty; public AdmissionApplicationStatus Status { get; set; } public string? DecisionReason { get; set; } public Guid? DecisionBy { get; set; } public DateTime? DecisionAt { get; set; } public DateTime? OfferExpiresAt { get; set; } public DateTime? OfferAcceptedAt { get; set; } public Guid? StudentId { get; set; } public Guid? OnboardingCaseId { get; set; } public DateTime? FinalizedAt { get; set; } public Guid? EnrolledStudentId { get; set; } public Guid? StudentEnrollmentId { get; set; } public Guid? CurrentStageInstanceId { get; set; } public DateTime? LastSavedAt { get; set; } public DateTime? SubmittedAt { get; set; } }
public partial class AdmissionApplicationStageDto { public Guid Id { get; set; } public Guid DefinitionStageId { get; set; } public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public AdmissionWorkflowStageType StageType { get; set; } public int DisplayOrder { get; set; } public AdmissionRuntimeStageStatus Status { get; set; } public bool AllowManualSkip { get; set; } public bool AssessmentEnabled { get; set; } public bool DocumentVerificationEnabled { get; set; } public bool EligibilityEnabled { get; set; } public bool SeatStageEnabled { get; set; } public DateTime? EnteredAt { get; set; } public DateTime? CompletedAt { get; set; } public Guid? CompletedBy { get; set; } public string? CompletionReason { get; set; } }
public class AdmissionWorkflowTransitionHistoryDto { public Guid Id { get; set; } public Guid FromStageInstanceId { get; set; } public Guid? ToStageInstanceId { get; set; } public Guid? TransitionDefinitionId { get; set; } public AdmissionWorkflowTransitionAction Action { get; set; } public string? Reason { get; set; } public string? ConditionEvaluationSnapshotJson { get; set; } public DateTime OccurredAt { get; set; } }
public class AdmissionWorkflowRuntimeTransitionDto { public Guid Id { get; set; } public Guid ToStageDefinitionId { get; set; } public Guid ToStageInstanceId { get; set; } public string ToStageName { get; set; } = string.Empty; public int Priority { get; set; } public bool IsDefault { get; set; } public string? ConditionRuleJson { get; set; } }
public class AdmissionApplicationWorkflowDto { public Guid WorkflowDefinitionId { get; set; } public AdmissionWorkflowInstanceStatus Status { get; set; } public DateTime StartedAt { get; set; } public DateTime? CompletedAt { get; set; } public Guid? CurrentStageInstanceId { get; set; } public string? NextRequiredAction { get; set; } public List<AdmissionApplicationStageDto> Stages { get; set; } = []; public List<AdmissionWorkflowRuntimeTransitionDto> AvailableTransitions { get; set; } = []; public List<AdmissionWorkflowTransitionHistoryDto> History { get; set; } = []; }
public class AdvanceAdmissionWorkflowDto { public Guid ExpectedCurrentStageInstanceId { get; set; } public Guid? TransitionDefinitionId { get; set; } public string IdempotencyKey { get; set; } = string.Empty; public string? ConcurrencyStamp { get; set; } public string? Reason { get; set; } public bool SkipCurrentStage { get; set; } public bool AdminOverride { get; set; } }
public class AdmissionFormValueDto { public string FieldCode { get; set; } = string.Empty; public string? ValueJson { get; set; } }
public class AdmissionApplicationFormDto { public Guid FormInstanceId { get; set; } public Guid FormDefinitionId { get; set; } public Guid AcademicYearId { get; set; } public int FormVersion { get; set; } public AdmissionFormInstanceStatus Status { get; set; } public DateTime? SubmittedAt { get; set; } public AdmissionFormDto Definition { get; set; } = new(); public List<AdmissionFormValueDto> Values { get; set; } = []; }
public class UpdateAdmissionApplicationFormValuesDto { public string? ConcurrencyStamp { get; set; } public List<AdmissionFormValueDto> Values { get; set; } = []; }
public class AdmissionApplicationSubmitResultDto { public AdmissionApplicationDto Application { get; set; } = new(); public List<string> ValidationErrors { get; set; } = []; public Dictionary<string, string> FieldErrors { get; set; } = []; }
public class AdmissionFormChangeHistoryDto { public Guid Id { get; set; } public string ChangeType { get; set; } = string.Empty; public string? ChangedFieldsJson { get; set; } public Guid? ChangedBy { get; set; } public DateTime OccurredAt { get; set; } }
public class AdmissionApplicationDecisionDto { public string? Reason { get; set; } public DateTimeOffset? OfferExpiresAt { get; set; } public Guid? CounsellorId { get; set; } public string? ConcurrencyStamp { get; set; } public string? IdempotencyKey { get; set; } public bool ConfirmTermsAccepted { get; set; } public bool ConfirmConditionsSatisfied { get; set; } }
public class EndAdmissionJourneyDto { public string Reason { get; set; } = string.Empty; public string ConcurrencyStamp { get; set; } = string.Empty; }
public partial class AdmissionApplicationDto { public bool CanEndJourney { get; set; } }
public class AdmissionApplicationDecisionHistoryDto { public Guid Id { get; set; } public AdmissionApplicationStatus Status { get; set; } public string? Reason { get; set; } public Guid? ActorId { get; set; } public string? ActorName { get; set; } public DateTime OccurredAt { get; set; } }
public class EnrollAdmissionApplicationDto { public string? ConcurrencyStamp { get; set; } public Guid? DivisionId { get; set; } }
public class AdmissionEnrollmentResultDto { public AdmissionApplicationDto Application { get; set; } = new(); public Guid StudentId { get; set; } public Guid StudentEnrollmentId { get; set; } public Guid AcademicYearId { get; set; } public Guid StandardId { get; set; } public Guid? DivisionId { get; set; } }
public class AdmissionEnrollmentDivisionOptionDto { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public int Strength { get; set; } public int OccupiedSeats { get; set; } public int AvailableSeats { get; set; } public bool IsAvailable { get; set; } }
public class AdmissionEnrollmentOptionsDto { public Guid StandardId { get; set; } public List<AdmissionEnrollmentDivisionOptionDto> Divisions { get; set; } = []; }

public class AdmissionFinalizationReadinessItemDto { public string Key { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public bool Passed { get; set; } public string Detail { get; set; } = string.Empty; }
public class AdmissionStudentMatchDto { public bool CanReviewProfileFields { get; set; } public bool CanApplyProfileFieldsDirectly { get; set; } public Guid StudentId { get; set; } public string StudentName { get; set; } = string.Empty; public DateTime? DateOfBirth { get; set; } public StudentLifeCycleStatusEnum Status { get; set; } public string? GrNumber { get; set; } public Guid? AcademicYearId { get; set; } }
public class AdmissionCapturedStudentFieldDto { public string Key { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
public class AdmissionFinalizationPreviewDto { public List<AdmissionCapturedStudentFieldDto> CapturedStudentFields { get; set; } = []; public bool CanReviewProfileFields { get; set; } public bool CanApplyProfileFieldsDirectly { get; set; } public string ProfileReviewDescription { get; set; } = string.Empty; public Guid ApplicationId { get; set; } public bool IsReady { get; set; } public Guid? LinkedStudentId { get; set; } public List<AdmissionFinalizationReadinessItemDto> Items { get; set; } = []; public List<AdmissionStudentMatchDto> PossibleMatches { get; set; } = []; }
public class FinalizeAdmissionApplicationDto { public bool ApplyStudentProfileFields { get; set; } public string? ConcurrencyStamp { get; set; } public string IdempotencyKey { get; set; } = string.Empty; public Guid? ExistingStudentId { get; set; } public bool ConfirmCreateNew { get; set; } public bool ConfirmGuardianContactVerified { get; set; } }
public class AdmissionFinalizationResultDto { public AdmissionApplicationDto Application { get; set; } = new(); public Guid StudentId { get; set; } public Guid OnboardingCaseId { get; set; } public bool ReusedExistingStudent { get; set; } }
