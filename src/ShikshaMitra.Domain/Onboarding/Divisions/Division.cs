using System;
using ShikshaMitra.Onboarding.Standards;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Onboarding.Divisions;

public class Division : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; }


    // Foreign key
    public Guid StandardId { get; set; }

    // Navigation property
    public Standard Standard { get; set; }
}