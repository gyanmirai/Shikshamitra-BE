using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Fees.Settings;

public class FeeCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid AcademicYearId { get; private set; }

    [Required]
    public string Name { get; private set; }

    public string Description { get; private set; }

    protected FeeCategory() { }

    public FeeCategory(Guid id, Guid? tenantId, Guid academicYearId, string name, string description) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        Name = name;
        Description = description;
    }

    public void Update(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
