using ShikshaMitra.Onboarding.EducationMediums;
using ShikshaMitra.Onboarding.Standards;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class EducationMediumStage : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid EducationMediumId { get; set; }
	public string Name { get; set; }

	// Navigation property
	public EducationMedium EducationMedium { get; set; }
	public ICollection<Standard> Standards { get; set; }
}