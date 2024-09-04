using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;

public class EducationMediumStagePermission : AggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? UserId { get; private set; }
	public Guid EducationMediumStageId { get; private set; }

	private EducationMediumStagePermission()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal EducationMediumStagePermission(
		Guid id,
		Guid? tenantId,
		Guid? userId,
		Guid educationMediumStageId)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		UserId = Check.NotNull(userId, nameof(userId));
		EducationMediumStageId = Check.NotNull(educationMediumStageId, nameof(educationMediumStageId));
	}
}