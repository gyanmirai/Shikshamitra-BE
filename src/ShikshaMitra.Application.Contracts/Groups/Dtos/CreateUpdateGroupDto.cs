using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;
using ShikshaMitra.Groups;

namespace ShikshaMitra.Groups.Dtos;

public class CreateGroupDto
{
    [Required]
    public string Name { get; set; }

    public string? Description { get; set; }

    [Required]
    public GroupAudienceType AudienceType { get; set; }

    public List<AcademicRule> AcademicRules { get; set; } = new();
    
    public List<Guid> Roles { get; set; } = new();
    
    public List<UserFilter> StudentFilters { get; set; } = new();
    
    public List<UserFilter> EmployeeFilters { get; set; } = new();
    
    public List<Guid> SelectedUserIds { get; set; } = new();
}

public class UpdateGroupDto : CreateGroupDto
{
}
