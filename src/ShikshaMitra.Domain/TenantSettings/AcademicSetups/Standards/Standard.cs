using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.Standards;

public class Standard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }
	public Guid EducationMediumStageId { get; }

	public string Name { get; internal set; }

	public int AgeLowerLimit { get; internal set; }


	private Standard()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal Standard(
		Guid id,
		Guid? tenantId,
		string name,
		int ageLowerLimit,
		Guid educationMediumStageId)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		EducationMediumStageId = Check.NotNull(educationMediumStageId, nameof(educationMediumStageId));
		AgeLowerLimit = ageLowerLimit;
		SetName(name);
	}

	internal Standard SetName(string name)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: StandardConsts.MaxNameLength);
		return this;
	}

	// Navigation property
	public EducationMediumStage EducationMediumStage { get; set; }
	public ICollection<Division> Divisions { get; set; }
}