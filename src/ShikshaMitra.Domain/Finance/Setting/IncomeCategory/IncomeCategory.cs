using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Finance.Setting.IncomeCategory;

public class IncomeCategory : FullAuditedAggregateRoot<Guid>
{
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; } // Stored as comma-separated values
    public bool IsActive { get; set; }

    protected IncomeCategory() { }

    public IncomeCategory(
        Guid id,
        Guid academicYearId,
        string name,
        string? description,
        string? tags,
        bool isActive = true)
        : base(id)
    {
        AcademicYearId = academicYearId;
        Name = name;
        Description = description;
        Tags = tags;
        IsActive = isActive;
    }
}
