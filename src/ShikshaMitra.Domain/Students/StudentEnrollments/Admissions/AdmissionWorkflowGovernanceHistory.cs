using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowGovernanceHistory : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid WorkflowDefinitionId { get; private set; }
	public AdmissionWorkflowGovernanceAction Action { get; private set; }
	public AdmissionWorkflowStatus? FromStatus { get; private set; }
	public AdmissionWorkflowStatus ToStatus { get; private set; }
	public Guid? ActorUserId { get; private set; }
	public Guid? AssignedApproverId { get; private set; }
	public string? Reason { get; private set; }
	public DateTime? EffectiveAt { get; private set; }
	public DateTime OccurredAt { get; private set; }

	private AdmissionWorkflowGovernanceHistory() { }

	public AdmissionWorkflowGovernanceHistory(Guid id, Guid? tenantId, Guid workflowDefinitionId,
		AdmissionWorkflowGovernanceAction action, AdmissionWorkflowStatus? fromStatus,
		AdmissionWorkflowStatus toStatus, Guid? actorUserId, string? reason, DateTime occurredAt,
		Guid? assignedApproverId = null, DateTime? effectiveAt = null) : base(id)
	{
		TenantId = tenantId;
		WorkflowDefinitionId = Check.NotDefaultOrNull<Guid>(workflowDefinitionId, nameof(workflowDefinitionId));
		Action = action;
		FromStatus = fromStatus;
		ToStatus = toStatus;
		ActorUserId = actorUserId;
		AssignedApproverId = assignedApproverId;
		Reason = string.IsNullOrWhiteSpace(reason) ? null : Check.Length(reason.Trim(), nameof(reason), AdmissionWorkflowConsts.MaxReasonLength);
		EffectiveAt = effectiveAt?.ToUniversalTime();
		OccurredAt = occurredAt.ToUniversalTime();
	}
}
