using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Setting;

public class ChargeCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public bool IsApprovalRequired { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    protected ChargeCategory() { }

    public ChargeCategory(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string name,
        bool isApprovalRequired,
        string? description,
        bool isActive = true)
        : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        Name = name;
        IsApprovalRequired = isApprovalRequired;
        Description = description;
        IsActive = isActive;
    }
}
