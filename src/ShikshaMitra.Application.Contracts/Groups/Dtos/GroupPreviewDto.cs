using System;
using System.Collections.Generic;

namespace ShikshaMitra.Groups.Dtos;

public class GroupPreviewInputDto
{
     public List<AcademicRule> AcademicRules { get; set; } = new();
    
    public List<Guid> Roles { get; set; } = new();
    
    public List<UserFilter> StudentFilters { get; set; } = new();
    
    public List<UserFilter> EmployeeFilters { get; set; } = new();
}

public class GroupPreviewResultDto
{
    public int TotalUsers { get; set; }
    public int Students { get; set; }
    public int Employees { get; set; }
}
