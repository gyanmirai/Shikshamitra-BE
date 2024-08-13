using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.EducationBoards;

public class EducationMediumStage : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public string Name { get; private set; }

	private EducationMediumStage()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal EducationMediumStage(
		Guid id,
		Guid? tenantId,
		string name,
		Guid educationMediumId)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		EducationMediumId = Check.NotNull(educationMediumId, nameof(educationMediumId));
		SetName(name);
	}


	internal EducationMediumStage SetName(string name)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: EducationMediumStageConsts.MaxNameLength);
		return this;
	}

	// Navigation property
	public EducationMedium EducationMedium { get; set; }
	public ICollection<Standard> Standards { get; set; }
}