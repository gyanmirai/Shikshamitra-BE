using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public class EducationMediumPermission : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? UserId { get; private set; }
	public Guid EducationMediumId { get; private set; }

	private EducationMediumPermission()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal EducationMediumPermission(
		Guid id,
		Guid? tenantId,
		Guid? userId,
		Guid educationMediumId)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		UserId = Check.NotNull(userId, nameof(userId));
		EducationMediumId = Check.NotNull(educationMediumId, nameof(educationMediumId));
	}
}