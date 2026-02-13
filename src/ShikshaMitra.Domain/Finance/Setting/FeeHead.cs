using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Setting;

public class FeeHead : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid AcademicYearId { get; private set; }

    public Guid FeeCategoryId { get; private set; }

    [Required]
    public string Name { get; private set; }

    public string Description { get; private set; }

    public bool IsActive { get; private set; }

    protected FeeHead() { }

    public FeeHead(Guid id, Guid? tenantId, Guid academicYearId, Guid feeCategoryId, string name, string description, bool isActive = true) : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        FeeCategoryId = feeCategoryId;
        Name = name;
        Description = description;
        IsActive = isActive;
    }

    public void Update(Guid feeCategoryId, string name, string description)
    {
        FeeCategoryId = feeCategoryId;
        Name = name;
        Description = description;
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
    }
}
