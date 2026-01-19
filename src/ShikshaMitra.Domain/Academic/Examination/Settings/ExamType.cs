using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Settings;

public class ExamType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public Guid AcademicMediumId { get; private set; }

    private ExamType()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    public ExamType(Guid id, Guid? tenantId, string name, string? description, Guid academicMediumId)
        : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        SetDescription(description);
        AcademicMediumId = academicMediumId;
    }

    public void SetName(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Name = name;
    }

    public void SetDescription(string? description)
    {
        Description = description;
    }
}
