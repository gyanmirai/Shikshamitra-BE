using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPolicyException : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid EvaluationId { get; private set; }
	public AdmissionCrmPolicyExceptionStatus Status { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public string EvidenceReference { get; private set; } = string.Empty;
	public Guid RequestedByUserId { get; private set; }
	public DateTime RequestedAt { get; private set; }
	public Guid? DecidedByUserId { get; private set; }
	public DateTime? DecidedAt { get; private set; }
	public string? DecisionReason { get; private set; }
	public DateTime? ExpiresAt { get; private set; }

	protected AdmissionCrmPolicyException() { }

	public AdmissionCrmPolicyException(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		Guid evaluationId, string reason, string evidenceReference, Guid requestedByUserId,
		DateTime requestedAt, DateTime? expiresAt) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		EvaluationId = evaluationId;
		Status = AdmissionCrmPolicyExceptionStatus.ApprovalPending;
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		EvidenceReference = Check.NotNullOrWhiteSpace(evidenceReference, nameof(evidenceReference), AdmissionCrmConsts.MaxFinanceReferenceLength);
		RequestedByUserId = requestedByUserId;
		RequestedAt = requestedAt.ToUniversalTime();
		ExpiresAt = expiresAt?.ToUniversalTime();
		if (ExpiresAt.HasValue && ExpiresAt <= RequestedAt)
			throw new BusinessException("AdmissionCrm:InvalidPolicyExceptionExpiry");
	}

	public void Decide(bool approve, string reason, Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmPolicyExceptionStatus.ApprovalPending)
			throw new BusinessException("AdmissionCrm:PolicyExceptionNotAwaitingApproval");
		if (RequestedByUserId == userId)
			throw new BusinessException("AdmissionCrm:DistinctPolicyExceptionApproverRequired");
		DecidedByUserId = userId;
		DecidedAt = at.ToUniversalTime();
		DecisionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = approve ? AdmissionCrmPolicyExceptionStatus.Approved : AdmissionCrmPolicyExceptionStatus.Rejected;
	}
}
