using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Standards;

public class StandardPermission : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid? UserId { get; set; }
	public Guid StandardId { get; set; }

	private StandardPermission()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal StandardPermission(
		Guid id,
		Guid? tenantId,
		Guid? userId,
		Guid standardId)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		UserId = Check.NotNull(userId, nameof(userId));
		StandardId = Check.NotNull(standardId, nameof(standardId));
	}
}