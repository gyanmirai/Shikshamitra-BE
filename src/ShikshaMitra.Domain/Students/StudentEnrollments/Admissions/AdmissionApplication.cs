using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string ApplicationNumber { get; private set; }
	public Guid ApplicantId { get; private set; }
	public Guid? InquiryId { get; private set; }
	public Guid AdmissionCycleId { get; private set; }
	public Guid AdmissionOfferingId { get; private set; }
	public Guid WorkflowDefinitionId { get; private set; }
	public Guid FormDefinitionId { get; private set; }
	public Guid? AssignedCounsellorId { get; private set; }
	public AdmissionApplicationStatus Status { get; private set; }
	public string? DecisionReason { get; private set; }
	public Guid? DecisionBy { get; private set; }
	public DateTime? DecisionAt { get; private set; }
	public DateTime? OfferExpiresAt { get; private set; }
	public DateTime? OfferAcceptedAt { get; private set; }
	public Guid? EnrolledStudentId { get; private set; }
	public Guid? StudentEnrollmentId { get; private set; }
	public Guid? StudentId { get; private set; }
	public Guid? OnboardingCaseId { get; private set; }
	public DateTime? FinalizedAt { get; private set; }
	public Guid? CurrentStageInstanceId { get; private set; }
	public DateTime? LastSavedAt { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	protected AdmissionApplication() { }

	public AdmissionApplication(Guid id, Guid? tenantId, string applicationNumber, Guid applicantId, Guid? inquiryId,
		Guid cycleId, Guid offeringId, Guid workflowDefinitionId, Guid formDefinitionId) : base(id)
	{
		TenantId = tenantId; ApplicationNumber = Check.NotNullOrWhiteSpace(applicationNumber, nameof(applicationNumber)); ApplicantId = applicantId;
		InquiryId = inquiryId; AdmissionCycleId = cycleId; AdmissionOfferingId = offeringId; WorkflowDefinitionId = workflowDefinitionId;
		FormDefinitionId = formDefinitionId; Status = AdmissionApplicationStatus.Draft; LastSavedAt = DateTime.UtcNow;
	}

	public void SetCurrentStage(Guid stageInstanceId) => CurrentStageInstanceId = stageInstanceId;
	public void AssignCounsellor(Guid counsellorId) =>
		AssignedCounsellorId = Check.NotDefaultOrNull<Guid>(counsellorId, nameof(counsellorId));
	public void CompleteWorkflow() => CurrentStageInstanceId = null;
	public void RecordWorkflowStarted(Guid workflowInstanceId, Guid entryStageInstanceId) =>
		AddLocalEvent(new ApplicationWorkflowStartedEvent(Id, workflowInstanceId, WorkflowDefinitionId, entryStageInstanceId, TenantId));
	public void RecordStageEntered(Guid workflowInstanceId, Guid stageInstanceId) =>
		AddLocalEvent(new ApplicationWorkflowStageEnteredEvent(Id, workflowInstanceId, stageInstanceId, TenantId));
	public void RecordStageCompleted(Guid workflowInstanceId, Guid stageInstanceId) =>
		AddLocalEvent(new ApplicationWorkflowStageCompletedEvent(Id, workflowInstanceId, stageInstanceId, TenantId));
	public void RecordStageSkipped(Guid workflowInstanceId, Guid stageInstanceId, string reason) =>
		AddLocalEvent(new ApplicationWorkflowStageSkippedEvent(Id, workflowInstanceId, stageInstanceId, reason, TenantId));
	public void RecordTransitioned(Guid workflowInstanceId, Guid fromStageInstanceId, Guid toStageInstanceId, Guid? transitionDefinitionId) =>
		AddLocalEvent(new ApplicationWorkflowTransitionedEvent(Id, workflowInstanceId, fromStageInstanceId, toStageInstanceId, transitionDefinitionId, TenantId));
	public void RecordWorkflowCompleted(Guid workflowInstanceId, Guid terminalStageInstanceId) =>
		AddLocalEvent(new ApplicationWorkflowCompletedEvent(Id, workflowInstanceId, terminalStageInstanceId, TenantId));
	public void SaveDraft() { if (Status != AdmissionApplicationStatus.Draft) throw new BusinessException(AdmissionApplicationErrorCodes.AlreadySubmitted); LastSavedAt = DateTime.UtcNow; }
	public void Submit() { if (Status != AdmissionApplicationStatus.Draft) throw new BusinessException(AdmissionApplicationErrorCodes.AlreadySubmitted); Status = AdmissionApplicationStatus.Submitted; SubmittedAt = DateTime.UtcNow; LastSavedAt = SubmittedAt; }
	public void Reopen() { if (Status != AdmissionApplicationStatus.Submitted) throw new BusinessException(AdmissionApplicationErrorCodes.FormNotSubmitted); Status = AdmissionApplicationStatus.Draft; LastSavedAt = DateTime.UtcNow; }
	public void ReopenForManagedReview()
	{
		EnsureStatus(AdmissionApplicationStatus.Submitted, AdmissionApplicationStatus.InReview, AdmissionApplicationStatus.Waitlisted, AdmissionApplicationStatus.Approved);
		Status = AdmissionApplicationStatus.Draft; LastSavedAt = DateTime.UtcNow;
		DecisionBy = null; DecisionAt = null; DecisionReason = null;
	}

	public void MarkInReview(Guid? actorId, string? reason)
	{
		EnsureStatus(AdmissionApplicationStatus.Submitted);
		SetDecision(AdmissionApplicationStatus.InReview, actorId, reason, requireReason: false);
	}

	public void Approve(Guid? actorId, string? reason)
	{
		EnsureStatus(AdmissionApplicationStatus.Submitted, AdmissionApplicationStatus.InReview, AdmissionApplicationStatus.Waitlisted);
		SetDecision(AdmissionApplicationStatus.Approved, actorId, reason, requireReason: false);
	}

	public void Reject(Guid? actorId, string reason)
	{
		EnsureStatus(AdmissionApplicationStatus.Submitted, AdmissionApplicationStatus.InReview, AdmissionApplicationStatus.Waitlisted, AdmissionApplicationStatus.Approved);
		SetDecision(AdmissionApplicationStatus.Rejected, actorId, reason, requireReason: true);
	}

	public void Waitlist(Guid? actorId, string reason)
	{
		EnsureStatus(AdmissionApplicationStatus.Submitted, AdmissionApplicationStatus.InReview);
		SetDecision(AdmissionApplicationStatus.Waitlisted, actorId, reason, requireReason: true);
	}

	public void IssueOffer(DateTime expiresAt, Guid? actorId, string? reason)
	{
		EnsureStatus(AdmissionApplicationStatus.Approved);
		if (expiresAt <= DateTime.UtcNow)
			throw new BusinessException(AdmissionErrorCodes.ApplicationOfferExpiryInvalid);
		OfferExpiresAt = expiresAt;
		SetDecision(AdmissionApplicationStatus.Offered, actorId, reason, requireReason: false);
	}

	public void AcceptOffer(Guid? actorId, string? reason)
	{
		EnsureStatus(AdmissionApplicationStatus.Offered);
		if (OfferExpiresAt.HasValue && OfferExpiresAt.Value <= DateTime.UtcNow)
			throw new BusinessException(AdmissionErrorCodes.ApplicationOfferExpiryInvalid);
		OfferAcceptedAt = DateTime.UtcNow;
		SetDecision(AdmissionApplicationStatus.OfferAccepted, actorId, reason, requireReason: false);
	}

	public void DeclineOffer(Guid? actorId, string reason)
	{
		EnsureStatus(AdmissionApplicationStatus.Offered);
		SetDecision(AdmissionApplicationStatus.OfferDeclined, actorId, reason, requireReason: true);
	}

	public void ExtendOffer(DateTime expiresAt)
	{
		EnsureStatus(AdmissionApplicationStatus.Offered);
		if (expiresAt <= DateTime.UtcNow || !OfferExpiresAt.HasValue || expiresAt <= OfferExpiresAt.Value)
			throw new BusinessException(AdmissionErrorCodes.ApplicationOfferExpiryInvalid);
		OfferExpiresAt = expiresAt;
	}

	public void WithdrawOffer(Guid? actorId, string reason)
	{
		EnsureStatus(AdmissionApplicationStatus.Offered);
		SetDecision(AdmissionApplicationStatus.Approved, actorId, reason, requireReason: true);
	}

	public void ReissueOffer(DateTime expiresAt, Guid? actorId, string? reason)
	{
		var expired = Status == AdmissionApplicationStatus.Offered && OfferExpiresAt.HasValue && OfferExpiresAt.Value <= DateTime.UtcNow;
		if (!expired && Status != AdmissionApplicationStatus.OfferDeclined && Status != AdmissionApplicationStatus.Approved)
			throw new BusinessException(AdmissionErrorCodes.ApplicationDecisionInvalidState);
		if (expiresAt <= DateTime.UtcNow)
			throw new BusinessException(AdmissionErrorCodes.ApplicationOfferExpiryInvalid);
		OfferExpiresAt = expiresAt;
		OfferAcceptedAt = null;
		SetDecision(AdmissionApplicationStatus.Offered, actorId, reason, requireReason: false);
	}

	public void MarkEnrolled(Guid studentId, Guid studentEnrollmentId)
	{
		EnsureStatus(AdmissionApplicationStatus.OfferAccepted);
		if (EnrolledStudentId.HasValue || StudentEnrollmentId.HasValue)
			throw new BusinessException(AdmissionErrorCodes.ApplicationAlreadyEnrolled);
		EnrolledStudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		StudentEnrollmentId = Check.NotDefaultOrNull<Guid>(studentEnrollmentId, nameof(studentEnrollmentId));
		Status = AdmissionApplicationStatus.Enrolled;
	}

	public void FinalizeAdmission(Guid studentId, Guid onboardingCaseId, DateTime finalizedAt)
	{
		if (Status == AdmissionApplicationStatus.AdmissionFinalized &&
		    StudentId == studentId && OnboardingCaseId == onboardingCaseId)
			return;
		EnsureStatus(AdmissionApplicationStatus.OfferAccepted);
		CompleteFinalization(studentId, onboardingCaseId, finalizedAt);
	}

	public void FinalizeApprovedDirectAdmission(Guid studentId, Guid onboardingCaseId, DateTime finalizedAt)
	{
		if (Status == AdmissionApplicationStatus.AdmissionFinalized &&
		    StudentId == studentId && OnboardingCaseId == onboardingCaseId)
			return;
		EnsureStatus(AdmissionApplicationStatus.Approved);
		CompleteFinalization(studentId, onboardingCaseId, finalizedAt);
	}

	private void CompleteFinalization(Guid studentId, Guid onboardingCaseId, DateTime finalizedAt)
	{
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		OnboardingCaseId = Check.NotDefaultOrNull<Guid>(onboardingCaseId, nameof(onboardingCaseId));
		FinalizedAt = finalizedAt.ToUniversalTime();
		Status = AdmissionApplicationStatus.AdmissionFinalized;
	}

	public void LinkHistoricalOnboarding(Guid studentId, Guid onboardingCaseId)
	{
		if (Status != AdmissionApplicationStatus.Enrolled)
			throw new BusinessException(AdmissionErrorCodes.ApplicationDecisionInvalidState);
		if (StudentId.HasValue || OnboardingCaseId.HasValue)
		{
			if (StudentId == studentId && OnboardingCaseId == onboardingCaseId) return;
			throw new BusinessException(AdmissionErrorCodes.ApplicationAlreadyEnrolled);
		}
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		OnboardingCaseId = Check.NotDefaultOrNull<Guid>(onboardingCaseId, nameof(onboardingCaseId));
	}

	private void SetDecision(AdmissionApplicationStatus status, Guid? actorId, string? reason, bool requireReason)
	{
		if (requireReason && string.IsNullOrWhiteSpace(reason))
			throw new BusinessException(AdmissionErrorCodes.ApplicationDecisionReasonRequired);
		Status = status;
		DecisionBy = actorId;
		DecisionAt = DateTime.UtcNow;
		DecisionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
	}

	private void EnsureStatus(params AdmissionApplicationStatus[] allowed)
	{
		if (!allowed.Contains(Status))
			throw new BusinessException(AdmissionErrorCodes.ApplicationDecisionInvalidState);
	}
}
