using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.Divisions;
using ShikshaMitra.Onboarding.EducationBoards;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.Standards;

public class Standard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid EducationMediumStageId { get; set; }

	public string Name { get; set; }

	// Navigation property
	public EducationMediumStage EducationMediumStage { get; set; }
	public ICollection<Division> Divisions { get; set; }
}