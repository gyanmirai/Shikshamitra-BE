using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmHouseholdMembership : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid HouseholdId { get; private set; }
	public Guid PersonId { get; private set; }
	public AdmissionCrmHouseholdRole Role { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveUntil { get; private set; }
	public bool IsActive { get; private set; }

	protected AdmissionCrmHouseholdMembership()
	{
	}

	public AdmissionCrmHouseholdMembership(
		Guid id, Guid? tenantId, Guid householdId, Guid personId,
		AdmissionCrmHouseholdRole role, DateTime effectiveFrom) : base(id)
	{
		TenantId = tenantId;
		HouseholdId = householdId;
		PersonId = personId;
		Role = role;
		EffectiveFrom = effectiveFrom.ToUniversalTime();
		IsActive = true;
	}

	public void End(DateTime at)
	{
		var end = at.ToUniversalTime();
		if (end < EffectiveFrom)
		{
			throw new BusinessException("AdmissionCrm:InvalidHouseholdMembershipEnd");
		}
		EffectiveUntil = end;
		IsActive = false;
	}
}
