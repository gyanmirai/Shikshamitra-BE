using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;

public class AcademicYearPermission : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? UserId { get; private set; }
	public Guid AcademicYearId { get; private set; }

	private AcademicYearPermission()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal AcademicYearPermission(
		Guid id,
		Guid? tenantId,
		Guid? userId,
		Guid academicYearId)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		UserId = Check.NotNull(userId, nameof(userId));
		AcademicYearId = Check.NotNull(academicYearId, nameof(academicYearId));
	}
}