using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Divisions;

public class DivisionPermission : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? UserId { get; private set; }
	public Guid DivisionId { get; private set; }

	private DivisionPermission()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal DivisionPermission(
		Guid id,
		Guid? tenantId,
		Guid? userId,
		Guid divisionId)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		UserId = Check.NotNull(userId, nameof(userId));
		DivisionId = Check.NotNull(divisionId, nameof(divisionId));
	}
}