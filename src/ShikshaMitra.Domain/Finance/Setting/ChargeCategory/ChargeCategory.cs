using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Finance.Setting.ChargeCategory;

public class ChargeCategory : FullAuditedAggregateRoot<Guid>
{
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public bool IsApprovalRequired { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    protected ChargeCategory() { }

    public ChargeCategory(
        Guid id,
        Guid academicYearId,
        string name,
        bool isApprovalRequired,
        string? description,
        bool isActive = true)
        : base(id)
    {
        AcademicYearId = academicYearId;
        Name = name;
        IsApprovalRequired = isApprovalRequired;
        Description = description;
        IsActive = isActive;
    }
}
