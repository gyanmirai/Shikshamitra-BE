using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public class ClassTeacherWorkspaceDto
{
    public List<ClassTeacherRowDto> Items { get; set; } = [];
    public ClassTeacherSummaryDto Summary { get; set; } = new();
    public List<ClassTeacherEligibleTeacherDto> EligibleTeachers { get; set; } = [];
}
