using System;
using System.Collections.Generic;
using ShikshaMitra.Groups;

namespace ShikshaMitra.Groups.Dtos;

public class GroupDetailsResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public int TotalAudience { get; set; }
    public int Students { get; set; }
    public int Employees { get; set; }
    public List<AcademicRuleResponseDto> AcademicRules { get; set; } = new();
    public List<UserFilter> StudentFilters { get; set; } = new();
    public List<UserFilter> EmployeeFilters { get; set; } = new();
    public List<RoleDto> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AcademicRuleResponseDto
{
    public List<IdNameDto> Boards { get; set; } = new();
    public List<MediumDto> Mediums { get; set; } = new();
    public List<StageDto> Stages { get; set; } = new();
    public List<ClassDto> Classes { get; set; } = new();
    public List<DivisionDto> Divisions { get; set; } = new();
}

public class IdNameDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}

public class MediumDto : IdNameDto
{
    public Guid BoardId { get; set; }
}

public class StageDto : IdNameDto
{
    public Guid MediumId { get; set; }
}

public class ClassDto : IdNameDto
{
    public Guid StageId { get; set; }
}

public class DivisionDto : IdNameDto
{
    public Guid ClassId { get; set; }
}

public class RoleDto : IdNameDto
{
    public string Code { get; set; }
}
