using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSlaHold : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public AdmissionCrmSlaHoldReason HoldReason { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public DateTime StartedAt { get; private set; }
	public DateTime? ResumedAt { get; private set; }
	public Guid StartedByUserId { get; private set; }
	public Guid? ResumedByUserId { get; private set; }
	public string? ResumeReason { get; private set; }
	public bool IsActive => !ResumedAt.HasValue;

	protected AdmissionCrmSlaHold() { }
	public AdmissionCrmSlaHold(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		AdmissionCrmSlaHoldReason holdReason, string reason, Guid userId, DateTime at) : base(id)
	{
		TenantId = tenantId; AcademicYearId = academicYearId; LeadId = leadId; HoldReason = holdReason;
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		StartedByUserId = userId; StartedAt = at.ToUniversalTime();
	}
	public void Resume(Guid userId, string reason, DateTime at)
	{
		if (ResumedAt.HasValue) throw new BusinessException("AdmissionCrm:SlaHoldAlreadyResumed");
		ResumeReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		ResumedByUserId = userId; ResumedAt = at.ToUniversalTime();
	}
}
