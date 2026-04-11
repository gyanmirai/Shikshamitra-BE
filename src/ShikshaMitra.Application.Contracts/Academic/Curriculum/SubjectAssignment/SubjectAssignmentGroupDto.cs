using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAssignmentGroupDto
{
    public Guid Id { get; set; }
    public SubjectType SubjectType { get; set; }
    public Guid? ElectiveGroupId { get; set; }
    public string? ElectiveGroupName { get; set; }
    public string Color { get; set; }
    public SubjectStatus Status { get; set; }
    public List<SubjectLiteDto> Subjects { get; set; } = new();
}
