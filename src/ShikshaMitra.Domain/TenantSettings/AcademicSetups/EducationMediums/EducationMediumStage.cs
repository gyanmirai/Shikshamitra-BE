using ShikshaMitra.Onboarding.EducationMediums;
using System;
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
}