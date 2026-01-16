using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp;

namespace ShikshaMitra.Groups;

public class Group : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public GroupAudienceType AudienceType { get; private set; }

    // Stored as JSON or related tables
    public List<AcademicRule> AcademicRules { get; set; } = new();
    public List<Guid> Roles { get; set; } = new();
    public List<UserFilter> StudentFilters { get; set; } = new();
    public List<UserFilter> EmployeeFilters { get; set; } = new();
    
    // For Name Based groups
    public List<Guid> SelectedUserIds { get; set; } = new();

    protected Group()
    {
    }

    public Group(Guid id, Guid? tenantId, string name, string? description, GroupAudienceType audienceType)
        : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        SetDescription(description);
        AudienceType = audienceType;
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

    public void SetAudienceType(GroupAudienceType audienceType)
    {
        AudienceType = audienceType;
    }
}
