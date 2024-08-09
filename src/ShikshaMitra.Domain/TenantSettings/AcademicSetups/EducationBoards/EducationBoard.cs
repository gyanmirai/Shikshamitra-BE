using ShikshaMitra.Onboarding.EducationMediums;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.EducationBoards;

public class EducationBoard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string Name { get; set; }
	public ICollection<EducationMedium> EducationMediums { get; set; }
}