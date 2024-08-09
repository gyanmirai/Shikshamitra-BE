using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.EducationBoards;
using ShikshaMitra.Onboarding.Standards;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.EducationMediums;

public class EducationMedium : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }

	public string Name { get; set; }

	// Foreign key for EducationBoard
	public Guid EducationBoardId { get; set; }

	// Navigation property
	public EducationBoard EducationBoard { get; set; }

	public ICollection<Standard> Standards { get; set; }

	public ICollection<EducationMediumStage> EducationMediumStages { get; set; }
}