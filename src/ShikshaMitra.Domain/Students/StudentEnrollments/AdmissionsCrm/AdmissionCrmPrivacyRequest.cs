using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPrivacyRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public AdmissionCrmPrivacyRequestType Type { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public string PreviewJson { get; private set; } = "{}";
	public AdmissionCrmPrivacyRequestStatus Status { get; private set; }
	public Guid RequestedByUserId { get; private set; }
	public DateTime RequestedAt { get; private set; }
	public Guid? DecidedByUserId { get; private set; }
	public DateTime? DecidedAt { get; private set; }
	public string? DecisionReason { get; private set; }
	public string? CompletionReference { get; private set; }
	public DateTime? CompletedAt { get; private set; }

	protected AdmissionCrmPrivacyRequest() { }
	public AdmissionCrmPrivacyRequest(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		AdmissionCrmPrivacyRequestType type, string reason, string previewJson, Guid userId, DateTime at) : base(id)
	{
		TenantId = tenantId; AcademicYearId = academicYearId; LeadId = leadId; Type = type;
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		PreviewJson = Check.NotNullOrWhiteSpace(previewJson, nameof(previewJson), AdmissionCrmConsts.MaxActivationStepsJsonLength);
		RequestedByUserId = userId; RequestedAt = at.ToUniversalTime(); Status = AdmissionCrmPrivacyRequestStatus.ApprovalPending;
	}
	public void Decide(bool approve, Guid userId, string reason, DateTime at)
	{
		if (Status != AdmissionCrmPrivacyRequestStatus.ApprovalPending) throw new BusinessException("AdmissionCrm:PrivacyRequestNotAwaitingApproval");
		if (RequestedByUserId == userId) throw new BusinessException("AdmissionCrm:DistinctPrivacyRequestApproverRequired");
		DecidedByUserId = userId; DecidedAt = at.ToUniversalTime();
		DecisionReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = approve ? AdmissionCrmPrivacyRequestStatus.Approved : AdmissionCrmPrivacyRequestStatus.Rejected;
	}
	public void Complete(string reference, DateTime at)
	{
		if (Status != AdmissionCrmPrivacyRequestStatus.Approved) throw new BusinessException("AdmissionCrm:ApprovedPrivacyRequestRequired");
		CompletionReference = Check.NotNullOrWhiteSpace(reference, nameof(reference), AdmissionCrmConsts.MaxNameLength);
		CompletedAt = at.ToUniversalTime(); Status = AdmissionCrmPrivacyRequestStatus.Completed;
	}
}
