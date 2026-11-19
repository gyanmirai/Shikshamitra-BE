using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationWorkflowInstance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid WorkflowDefinitionId { get; private set; }
	public AdmissionWorkflowInstanceStatus Status { get; private set; }
	public DateTime StartedAt { get; private set; }
	public DateTime? CompletedAt { get; private set; }

	protected AdmissionApplicationWorkflowInstance() { }

	public AdmissionApplicationWorkflowInstance(Guid id, Guid? tenantId, Guid applicationId, Guid workflowDefinitionId) : base(id)
	{
		TenantId = tenantId;
		ApplicationId = applicationId;
		WorkflowDefinitionId = workflowDefinitionId;
		Status = AdmissionWorkflowInstanceStatus.Active;
		StartedAt = DateTime.UtcNow;
	}

	public void Complete()
	{
		if (Status != AdmissionWorkflowInstanceStatus.Active)
			throw new BusinessException(AdmissionApplicationErrorCodes.WorkflowAlreadyCompleted);
		Status = AdmissionWorkflowInstanceStatus.Completed;
		CompletedAt = DateTime.UtcNow;
	}

	public void Cancel()
	{
		if (Status == AdmissionWorkflowInstanceStatus.Cancelled) return;
		if (Status is not (AdmissionWorkflowInstanceStatus.Active or AdmissionWorkflowInstanceStatus.Completed))
			throw new BusinessException(AdmissionApplicationErrorCodes.WorkflowAlreadyCompleted);
		Status = AdmissionWorkflowInstanceStatus.Cancelled;
		CompletedAt ??= DateTime.UtcNow;
	}

	public void Resume(AdmissionJourneyClosureSnapshot snapshot)
	{
		if (Status != AdmissionWorkflowInstanceStatus.Cancelled || snapshot.WorkflowId != Id ||
			snapshot.WorkflowDefinitionId != WorkflowDefinitionId || snapshot.WorkflowStatus is not
			(AdmissionWorkflowInstanceStatus.Active or AdmissionWorkflowInstanceStatus.Completed))
			throw new BusinessException(AdmissionJourneyResumeErrorCodes.Blocked, "The saved workflow cannot be restored.");
		Status = snapshot.WorkflowStatus;
		CompletedAt = snapshot.WorkflowCompletedAt;
	}
}

public class AdmissionApplicationStageInstance : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid WorkflowInstanceId { get; private set; }
	public Guid DefinitionStageId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public AdmissionWorkflowStageType StageType { get; private set; }
	public int DisplayOrder { get; private set; }
	public AdmissionRuntimeStageStatus Status { get; private set; }
	public DateTime? EnteredAt { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public Guid? CompletedBy { get; private set; }
	public string? CompletionReason { get; private set; }

	protected AdmissionApplicationStageInstance() { }

	public AdmissionApplicationStageInstance(Guid id, Guid? tenantId, Guid workflowInstanceId, Guid definitionStageId, string code, string name, AdmissionWorkflowStageType stageType, int displayOrder, bool active) : base(id)
	{
		TenantId = tenantId;
		WorkflowInstanceId = workflowInstanceId;
		DefinitionStageId = definitionStageId;
		Code = Check.NotNullOrWhiteSpace(code, nameof(code));
		Name = Check.NotNullOrWhiteSpace(name, nameof(name));
		StageType = stageType;
		DisplayOrder = displayOrder;
		Status = active ? AdmissionRuntimeStageStatus.Active : AdmissionRuntimeStageStatus.Pending;
		if (active) EnteredAt = DateTime.UtcNow;
	}

	public void Complete(Guid? completedBy = null, string? reason = null)
	{
		EnsureActive();
		Status = AdmissionRuntimeStageStatus.Completed;
		CompletedAt = DateTime.UtcNow;
		CompletedBy = completedBy;
		CompletionReason = NormalizeReason(reason);
	}

	public void Skip(Guid? completedBy, string reason)
	{
		EnsureActive();
		Status = AdmissionRuntimeStageStatus.Skipped;
		CompletedAt = DateTime.UtcNow;
		CompletedBy = completedBy;
		CompletionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 512).Trim();
	}

	public void Enter()
	{
		if (Status != AdmissionRuntimeStageStatus.Pending)
			throw new BusinessException(AdmissionApplicationErrorCodes.InvalidCurrentStage);
		Status = AdmissionRuntimeStageStatus.Active;
		EnteredAt = DateTime.UtcNow;
	}

	public void Cancel(Guid actorId, string reason)
	{
		if (Status is not (AdmissionRuntimeStageStatus.Active or AdmissionRuntimeStageStatus.Pending)) return;
		Status = AdmissionRuntimeStageStatus.Cancelled;
		CompletedAt = DateTime.UtcNow;
		CompletedBy = actorId;
		CompletionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 512).Trim();
	}

	private void EnsureActive()
	{
		if (Status != AdmissionRuntimeStageStatus.Active)
			throw new BusinessException(AdmissionApplicationErrorCodes.InvalidCurrentStage);
	}

	public void ResumeCancelled(bool wasActive)
	{
		if (Status != AdmissionRuntimeStageStatus.Cancelled || wasActive != EnteredAt.HasValue)
			throw new BusinessException(AdmissionJourneyResumeErrorCodes.Blocked, "The saved stage cannot be restored.");
		Status = wasActive ? AdmissionRuntimeStageStatus.Active : AdmissionRuntimeStageStatus.Pending;
		CompletedAt = null; CompletedBy = null; CompletionReason = null;
	}

	private static string? NormalizeReason(string? reason) =>
		string.IsNullOrWhiteSpace(reason) ? null : Check.Length(reason.Trim(), nameof(reason), 512);
}

public class AdmissionWorkflowTransitionHistory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public void RecordHandler(Guid? actorId, string? actorName)
	{
		CreatorId = actorId;
		ExtraProperties["ActorName"] = actorName;
	}
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid WorkflowInstanceId { get; private set; }
	public Guid FromStageInstanceId { get; private set; }
	public Guid? ToStageInstanceId { get; private set; }
	public Guid? TransitionDefinitionId { get; private set; }
	public AdmissionWorkflowTransitionAction Action { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string? Reason { get; private set; }
	public string? ConditionEvaluationSnapshotJson { get; private set; }
	public DateTime OccurredAt { get; private set; }

	protected AdmissionWorkflowTransitionHistory() { }

	public AdmissionWorkflowTransitionHistory(
		Guid id,
		Guid? tenantId,
		Guid applicationId,
		Guid workflowInstanceId,
		Guid fromStageInstanceId,
		Guid? toStageInstanceId,
		Guid? transitionDefinitionId,
		AdmissionWorkflowTransitionAction action,
		string idempotencyKey,
		string? reason,
		string? conditionEvaluationSnapshotJson = null) : base(id)
	{
		TenantId = tenantId;
		ApplicationId = applicationId;
		WorkflowInstanceId = workflowInstanceId;
		FromStageInstanceId = fromStageInstanceId;
		ToStageInstanceId = toStageInstanceId;
		TransitionDefinitionId = transitionDefinitionId;
		Action = action;
		IdempotencyKey = Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey), 128);
		Reason = reason?.Trim();
		ConditionEvaluationSnapshotJson = conditionEvaluationSnapshotJson;
		OccurredAt = DateTime.UtcNow;
	}
}

public class AdmissionFormInstance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public const string EffectiveSchemaKey = "AdmissionEffectiveFormSchemaV1";
	public const string EffectiveSchemaEnabledKey = "AdmissionEffectiveFormEnabled";

	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid FormDefinitionId { get; private set; }
	public int FormVersion { get; private set; }
	public AdmissionFormInstanceStatus Status { get; private set; }
	public DateTime StartedAt { get; private set; }
	public DateTime LastSavedAt { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	public Guid? SubmittedBy { get; private set; }
	protected AdmissionFormInstance() { }
	public AdmissionFormInstance(Guid id, Guid? tenantId, Guid applicationId, Guid formDefinitionId, int formVersion) : base(id)
	{
		TenantId = tenantId; ApplicationId = applicationId; FormDefinitionId = formDefinitionId; FormVersion = formVersion; Status = AdmissionFormInstanceStatus.Draft; StartedAt = DateTime.UtcNow; LastSavedAt = StartedAt;
	}
	public void MarkSaved() { if (Status == AdmissionFormInstanceStatus.Submitted) throw new BusinessException(AdmissionApplicationErrorCodes.AlreadySubmitted); LastSavedAt = DateTime.UtcNow; }
	public void Submit(Guid? submittedBy = null) { if (Status == AdmissionFormInstanceStatus.Submitted) throw new BusinessException(AdmissionApplicationErrorCodes.AlreadySubmitted); Status = AdmissionFormInstanceStatus.Submitted; SubmittedAt = DateTime.UtcNow; SubmittedBy = submittedBy; LastSavedAt = SubmittedAt.Value; }
	public void Reopen() { if (Status != AdmissionFormInstanceStatus.Submitted) throw new BusinessException(AdmissionApplicationErrorCodes.FormNotSubmitted); Status = AdmissionFormInstanceStatus.Reopened; }

	public void FreezeEffectiveSchema(string snapshotJson)
	{
		if (Status != AdmissionFormInstanceStatus.Draft || ExtraProperties.ContainsKey(EffectiveSchemaKey) ||
			ExtraProperties.ContainsKey(EffectiveSchemaEnabledKey))
			throw new BusinessException(AdmissionFormInheritanceErrorCodes.SnapshotImmutable);
		ExtraProperties[EffectiveSchemaKey] = Check.NotNullOrWhiteSpace(snapshotJson, nameof(snapshotJson));
		ExtraProperties[EffectiveSchemaEnabledKey] = true;
	}
}

public class AdmissionFormFieldValue : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid FormInstanceId { get; private set; }
	public string FieldCode { get; private set; } = string.Empty;
	public string? ValueJson { get; private set; }
	public DateTime LastModifiedAt { get; private set; }
	protected AdmissionFormFieldValue() { }
	public AdmissionFormFieldValue(Guid id, Guid? tenantId, Guid formInstanceId, string fieldCode, string? valueJson) : base(id)
	{
		TenantId = tenantId; FormInstanceId = formInstanceId; FieldCode = Check.NotNullOrWhiteSpace(fieldCode, nameof(fieldCode)); SetValue(valueJson);
	}
	public void SetValue(string? valueJson) { ValueJson = valueJson; LastModifiedAt = DateTime.UtcNow; }
}

public class AdmissionFormSubmission : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid FormInstanceId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public int AttemptNumber { get; private set; }
	public DateTime SubmittedAt { get; private set; }
	public Guid? SubmittedBy { get; private set; }
	public string SnapshotJson { get; private set; } = string.Empty;
	protected AdmissionFormSubmission() { }
	public AdmissionFormSubmission(Guid id, Guid? tenantId, Guid formInstanceId, Guid applicationId, int attemptNumber, Guid? submittedBy, string snapshotJson) : base(id)
	{
		TenantId = tenantId; FormInstanceId = formInstanceId; ApplicationId = applicationId; AttemptNumber = attemptNumber; SubmittedBy = submittedBy; SnapshotJson = Check.NotNullOrWhiteSpace(snapshotJson, nameof(snapshotJson)); SubmittedAt = DateTime.UtcNow; IsDeleted = false;
	}
}

public class AdmissionFormChangeHistory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid FormInstanceId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public string ChangeType { get; private set; } = string.Empty;
	public string? ChangedFieldsJson { get; private set; }
	public Guid? ChangedBy { get; private set; }
	public DateTime OccurredAt { get; private set; }
	protected AdmissionFormChangeHistory() { }
	public AdmissionFormChangeHistory(Guid id, Guid? tenantId, Guid formInstanceId, Guid applicationId, string changeType, string? changedFieldsJson, Guid? changedBy) : base(id)
	{
		TenantId = tenantId; FormInstanceId = formInstanceId; ApplicationId = applicationId; ChangeType = Check.NotNullOrWhiteSpace(changeType, nameof(changeType)); ChangedFieldsJson = changedFieldsJson; ChangedBy = changedBy; OccurredAt = DateTime.UtcNow; IsDeleted = false;
	}
}

public class AdmissionActivity : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public void RecordHandler(Guid? actorId, string? actorName)
	{
		CreatorId = actorId;
		MetadataJson = StudentJourneyActorMetadata.Capture(MetadataJson, actorId, actorName);
	}
	public Guid? TenantId { get; private set; }
	public Guid? ApplicantId { get; private set; }
	public Guid? ApplicationId { get; private set; }
	public Guid? InquiryId { get; private set; }
	public string ActivityType { get; private set; } = string.Empty;
	public string Description { get; private set; } = string.Empty;
	public DateTime OccurredAt { get; private set; }
	public string? MetadataJson { get; private set; }
	protected AdmissionActivity() { }
	public AdmissionActivity(Guid id, Guid? tenantId, string activityType, string description, Guid? applicantId = null, Guid? applicationId = null, Guid? inquiryId = null, string? metadataJson = null) : base(id)
	{
		TenantId = tenantId; ActivityType = Check.NotNullOrWhiteSpace(activityType, nameof(activityType)); Description = Check.NotNullOrWhiteSpace(description, nameof(description)); ApplicantId = applicantId; ApplicationId = applicationId; InquiryId = inquiryId; MetadataJson = metadataJson; OccurredAt = DateTime.UtcNow;
	}
}
