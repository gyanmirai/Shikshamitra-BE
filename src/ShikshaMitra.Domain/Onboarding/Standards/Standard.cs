using System;
using System.Collections.Generic;
using ShikshaMitra.Onboarding.Divisions;
using ShikshaMitra.Onboarding.EducationalMediums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.Standards;

public class Standard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public string Name { get; set; }
    public ICollection<Division> Divisions { get; set; }

    public Guid? TenantId { get; set; }

    // Foreign key
    public Guid EducationalMediumId { get; set; }

    // Navigation property
    public EducationalMedium EducationalMedium { get; set; }
}