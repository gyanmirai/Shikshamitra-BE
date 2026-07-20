using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Curriculum;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class AssignTeachersDto
{
    public Guid StandardId { get; set; }
    public Guid DivisionId { get; set; }
    public string Day { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public SubjectType SubjectType { get; set; }
    public Guid? ElectiveGroupId { get; set; }
    public List<TeacherAssignmentItemDto> Subjects { get; set; } = [];
}

public class TeacherAssignmentItemDto
{
    public Guid SubjectId { get; set; }
    public Guid? TeacherId { get; set; }
}
