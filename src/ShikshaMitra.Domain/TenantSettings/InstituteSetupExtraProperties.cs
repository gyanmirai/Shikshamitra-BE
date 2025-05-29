using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings;

public class InstituteSetupExtraProperties : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public InstituteSetupState State { get; set; }

	protected InstituteSetupExtraProperties()
	{
	}

	public InstituteSetupExtraProperties(Guid? tenantId, InstituteSetupState state)
	{
		TenantId = tenantId;
		State = state;
	}

	public void UpdateState(InstituteSetupState newState)
	{
		if (newState < State)
		{
			throw new InvalidOperationException("Cannot move to a previous state.");
		}
		State = newState;
	}
}
