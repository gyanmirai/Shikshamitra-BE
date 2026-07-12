using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionDecision : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid? SeatPoolId { get; private set; }
	public Guid? OfferId { get; private set; }
	public AdmissionCrmAdmissionDecisionStatus Status { get; private set; }
	public string? Reason { get; private set; }
	public bool OverrideUsed { get; private set; }
	public string? OverrideReason { get; private set; }
	public Guid? DecidedByUserId { get; private set; }
	public DateTime? DecidedAt { get; private set; }

	protected AdmissionCrmAdmissionDecision()
	{
	}

	public AdmissionCrmAdmissionDecision(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		Status = AdmissionCrmAdmissionDecisionStatus.Pending;
	}

	public void Decide(
		AdmissionCrmAdmissionDecisionStatus status,
		Guid? seatPoolId,
		Guid? offerId,
		string reason,
		bool overrideUsed,
		string? overrideReason,
		Guid userId,
		DateTime decidedAt)
	{
		if (status == AdmissionCrmAdmissionDecisionStatus.Pending)
		{
			throw new BusinessException("AdmissionCrm:AdmissionDecisionRequired");
		}
		if (overrideUsed && string.IsNullOrWhiteSpace(overrideReason))
		{
			throw new BusinessException("AdmissionCrm:AdmissionDecisionOverrideReasonRequired");
		}

		Status = status;
		SeatPoolId = seatPoolId;
		OfferId = offerId;
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		OverrideUsed = overrideUsed;
		OverrideReason = string.IsNullOrWhiteSpace(overrideReason)
			? null
			: Check.Length(overrideReason.Trim(), nameof(overrideReason), AdmissionCrmConsts.MaxNotesLength);
		DecidedByUserId = userId;
		DecidedAt = decidedAt.ToUniversalTime();
	}
}
