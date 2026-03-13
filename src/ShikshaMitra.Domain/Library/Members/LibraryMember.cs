using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Members;

public class LibraryMember : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; set; }
	public string MemberIdentifier { get; set; }
	public bool IsActive { get; set; } = true;

	private LibraryMember()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public LibraryMember(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		string memberIdentifier,
		bool isActive = true
	) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		MemberIdentifier = memberIdentifier;
		IsActive = isActive;
	}

	public void ChangeMemberStatus(bool isActive)
	{
		IsActive = isActive;
	}
}
