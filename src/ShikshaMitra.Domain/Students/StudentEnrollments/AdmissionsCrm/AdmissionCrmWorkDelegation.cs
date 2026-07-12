using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmWorkDelegation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid DelegatorUserId { get; private set; }
	public Guid DelegateUserId { get; private set; }
	public string QueueTypesJson { get; private set; } = "[]";
	public DateTime StartsAt { get; private set; }
	public DateTime EndsAt { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public AdmissionCrmDelegationStatus Status { get; private set; }

	protected AdmissionCrmWorkDelegation() { }
	public AdmissionCrmWorkDelegation(Guid id, Guid? tenantId, Guid academicYearId, Guid delegatorUserId,
		Guid delegateUserId, string queueTypesJson, DateTime startsAt, DateTime endsAt, string reason, DateTime now) : base(id)
	{
		if (delegatorUserId == delegateUserId) throw new BusinessException("AdmissionCrm:DelegationToSelfNotAllowed");
		StartsAt = startsAt.ToUniversalTime(); EndsAt = endsAt.ToUniversalTime();
		if (EndsAt <= StartsAt) throw new BusinessException("AdmissionCrm:InvalidDelegationPeriod");
		TenantId = tenantId; AcademicYearId = academicYearId; DelegatorUserId = delegatorUserId;
		DelegateUserId = delegateUserId; QueueTypesJson = Check.NotNullOrWhiteSpace(queueTypesJson, nameof(queueTypesJson), 2000);
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = now.ToUniversalTime() < StartsAt ? AdmissionCrmDelegationStatus.Scheduled : AdmissionCrmDelegationStatus.Active;
	}
	public void Refresh(DateTime now)
	{
		if (Status == AdmissionCrmDelegationStatus.Cancelled) return;
		var value = now.ToUniversalTime();
		Status = value < StartsAt ? AdmissionCrmDelegationStatus.Scheduled :
			value <= EndsAt ? AdmissionCrmDelegationStatus.Active : AdmissionCrmDelegationStatus.Expired;
	}
	public void Cancel() => Status = AdmissionCrmDelegationStatus.Cancelled;
}
