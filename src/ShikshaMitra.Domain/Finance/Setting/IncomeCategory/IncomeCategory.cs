using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Setting.IncomeCategory;

public class IncomeCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; } // Stored as comma-separated values
    public bool IsActive { get; set; }

    protected IncomeCategory() { }

    public IncomeCategory(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string name,
        string? description,
        string? tags,
        bool isActive = true)
        : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        Name = name;
        Description = description;
        Tags = tags;
        IsActive = isActive;
    }
}
