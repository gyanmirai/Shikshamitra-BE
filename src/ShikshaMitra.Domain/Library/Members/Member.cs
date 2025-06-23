using ShikshaMitra.Library.Fines;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Members;

public class Member : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
    public string Name { get; set; }
	public MemberRole Role { get; set; }

	private Member() { }
	public Member(
		Guid id,
		string name,
		MemberRole role,
		Guid? tenantId)
		: base(id)
	{
		Name = name;
		Role = role;
		TenantId = tenantId;
	}
}
