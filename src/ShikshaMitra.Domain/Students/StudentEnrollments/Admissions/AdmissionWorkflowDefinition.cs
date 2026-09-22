using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowDefinition : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public string Code { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionWorkflowStatus Status { get; private set; }
	public Guid? ParentVersionId { get; private set; }
	public string? Description { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public Guid? PublishedBy { get; private set; }
	public Guid? OwnerUserId { get; private set; }
	public DateTime? ValidatedAt { get; private set; }
	public Guid? ValidatedBy { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	public Guid? SubmittedBy { get; private set; }
	public Guid? AssignedApproverId { get; private set; }
	public DateTime? ApprovedAt { get; private set; }
	public Guid? ApprovedBy { get; private set; }
	public DateTime? ScheduledPublishAt { get; private set; }
	public string? ChangeReason { get; private set; }
	public Guid? RollbackSourceVersionId { get; private set; }

	protected AdmissionWorkflowDefinition()
	{
		// Keep the invariant true for every construction path, including the
		// protected path used by serializers/ORM tooling.
		IsDeleted = false;
	}

	public AdmissionWorkflowDefinition(
		Guid id,
		Guid? tenantId,
		string name,
		string code,
		int version = 1,
		Guid? parentVersionId = null,
		string? description = null,
		Guid? ownerUserId = null) : base(id)
	{
		TenantId = tenantId;
		// FullAuditedAggregateRoot does not initialize the soft-delete flag in
		// every ABP persistence path. Set it explicitly so new definitions never
		// send NULL to the non-nullable PostgreSQL column.
		IsDeleted = false;
		SetDetails(name, code, description);
		Version = version;
		ParentVersionId = parentVersionId;
		OwnerUserId = ownerUserId;
		Status = AdmissionWorkflowStatus.Draft;
		AddLocalEvent(new AdmissionWorkflowCreatedEvent(Id, TenantId));
	}

	public void UpdateDetails(string name, string code, string? description = null)
	{
		PrepareForEdit();
		SetDetails(name, code, description);
	}

	public void RecordVersionCreated(Guid sourceWorkflowDefinitionId) =>
		AddLocalEvent(new AdmissionWorkflowVersionCreatedEvent(sourceWorkflowDefinitionId, Id, Version, TenantId));

	public void MarkValidated(Guid? validatedBy, DateTime validatedAt)
	{
		if (Status != AdmissionWorkflowStatus.Draft)
			throw InvalidTransition();
		Status = AdmissionWorkflowStatus.Validated;
		ValidatedBy = validatedBy;
		ValidatedAt = validatedAt.ToUniversalTime();
	}

	public void SubmitForApproval(Guid submittedBy, Guid approverId, string changeReason, DateTime submittedAt)
	{
		if (Status != AdmissionWorkflowStatus.Validated)
			throw InvalidTransition();
		if (submittedBy == approverId)
			throw new BusinessException(AdmissionWorkflowErrorCodes.IndependentApproverRequired);
		Status = AdmissionWorkflowStatus.SubmittedForApproval;
		SubmittedBy = submittedBy;
		SubmittedAt = submittedAt.ToUniversalTime();
		AssignedApproverId = approverId;
		ChangeReason = NormalizeReason(changeReason);
	}

	public void Approve(Guid approvedBy, DateTime approvedAt)
	{
		if (Status != AdmissionWorkflowStatus.SubmittedForApproval)
			throw InvalidTransition();
		if (SubmittedBy == approvedBy)
			throw new BusinessException(AdmissionWorkflowErrorCodes.IndependentApproverRequired);
		if (AssignedApproverId != approvedBy)
			throw new BusinessException(AdmissionWorkflowErrorCodes.AssignedApproverRequired);
		Status = AdmissionWorkflowStatus.Approved;
		ApprovedBy = approvedBy;
		ApprovedAt = approvedAt.ToUniversalTime();
	}

	public void Reject(Guid rejectedBy, string reason)
	{
		if (Status != AdmissionWorkflowStatus.SubmittedForApproval)
			throw InvalidTransition();
		if (SubmittedBy == rejectedBy)
			throw new BusinessException(AdmissionWorkflowErrorCodes.IndependentApproverRequired);
		if (AssignedApproverId != rejectedBy)
			throw new BusinessException(AdmissionWorkflowErrorCodes.AssignedApproverRequired);
		Status = AdmissionWorkflowStatus.Draft;
		ChangeReason = NormalizeReason(reason);
		ClearApprovalState(clearValidation: true);
	}

	public void Schedule(DateTime effectiveAt)
	{
		if (Status != AdmissionWorkflowStatus.Approved && Status != AdmissionWorkflowStatus.Validated)
			throw InvalidTransition();
		Status = AdmissionWorkflowStatus.Scheduled;
		ScheduledPublishAt = effectiveAt.ToUniversalTime();
	}

	public void Publish(Guid? publishedBy, DateTime? publishedAt = null, string? changeReason = null)
	{
		if (Status is not (AdmissionWorkflowStatus.Draft or AdmissionWorkflowStatus.Validated or
			AdmissionWorkflowStatus.Approved or AdmissionWorkflowStatus.Scheduled))
			throw InvalidTransition();
		Status = AdmissionWorkflowStatus.Published;
		PublishedAt = (publishedAt ?? DateTime.UtcNow).ToUniversalTime();
		PublishedBy = publishedBy;
		ScheduledPublishAt = null;
		if (!string.IsNullOrWhiteSpace(changeReason))
			ChangeReason = NormalizeReason(changeReason);
		AddLocalEvent(new AdmissionWorkflowPublishedEvent(Id, Version, TenantId));
	}

	public void Supersede()
	{
		if (Status != AdmissionWorkflowStatus.Published)
			throw InvalidTransition();
		Status = AdmissionWorkflowStatus.Superseded;
	}

	public void RecordRollbackSource(Guid sourceVersionId, string reason)
	{
		EnsureDraft();
		RollbackSourceVersionId = sourceVersionId;
		ChangeReason = NormalizeReason(reason);
	}

	public void Archive()
	{
		if (Status is not (AdmissionWorkflowStatus.Published or AdmissionWorkflowStatus.Superseded))
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.PublishedImmutable);
		}

		Status = AdmissionWorkflowStatus.Archived;
		AddLocalEvent(new AdmissionWorkflowArchivedEvent(Id, Version, TenantId));
	}

	public void EnsureDraft()
	{
		if (Status != AdmissionWorkflowStatus.Draft)
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.PublishedImmutable);
		}
	}

	private void SetDetails(string name, string code, string? description)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionWorkflowConsts.MaxNameLength).Trim();
		Code = NormalizeCode(code);
		Description = string.IsNullOrWhiteSpace(description)
			? null
			: Check.Length(description.Trim(), nameof(description), AdmissionWorkflowConsts.MaxDescriptionLength);
	}

	private static string NormalizeCode(string code) => Check.NotNullOrWhiteSpace(code, nameof(code)).Trim().ToUpperInvariant();

	private static string NormalizeReason(string reason) =>
		Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionWorkflowConsts.MaxReasonLength).Trim();

	private void ClearApprovalState(bool clearValidation)
	{
		SubmittedAt = null;
		SubmittedBy = null;
		AssignedApproverId = null;
		ApprovedAt = null;
		ApprovedBy = null;
		ScheduledPublishAt = null;
		if (!clearValidation) return;
		ValidatedAt = null;
		ValidatedBy = null;
	}

	public void PrepareForEdit()
	{
		if (Status == AdmissionWorkflowStatus.Validated)
		{
			Status = AdmissionWorkflowStatus.Draft;
			ClearApprovalState(clearValidation: true);
			return;
		}

		EnsureDraft();
	}

	private static BusinessException InvalidTransition() =>
		new(AdmissionWorkflowErrorCodes.InvalidGovernanceTransition,
			"The workflow cannot perform this governance action from its current status.");
}
