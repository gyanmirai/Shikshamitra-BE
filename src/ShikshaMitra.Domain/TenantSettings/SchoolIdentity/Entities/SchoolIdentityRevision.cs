using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.SchoolIdentity;

public class SchoolIdentityRevision : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ProfileId { get; private set; }
	public string ChoicesJson { get; private set; } = "{}";
	public string Reason { get; private set; } = "";
	protected SchoolIdentityRevision() { }
	public SchoolIdentityRevision(Guid id, SchoolIdentityProfile profile, string reason) : base(id)
	{
		TenantId = profile.TenantId; ProfileId = profile.Id; ChoicesJson = profile.ChoicesJson; Reason = reason;
	}
}
