using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

public class TeacherWorkloadDto
{
    public Guid TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public int CurrentSubjectsCount { get; set; }
    public int CurrentWeeklyPeriods { get; set; }
    public int MaxAllowedWeeklyPeriods { get; set; }

    /// <summary>"Balanced", "High", "Overloaded"</summary>
    public string LoadStatus { get; set; } = "Balanced";

    public List<TeacherSubjectSummaryDto> AssignedSubjects { get; set; } = [];
}

public class TeacherSubjectSummaryDto
{
    public Guid AssignedSubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string? ClassName { get; set; }
    public int WeeklyPeriods { get; set; }
}
