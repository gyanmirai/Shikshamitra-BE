using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

public class CreateSubjectDto
{
    public Guid ClassId { get; set; }
    required public string SubjectCode { get; set; }
    required public string SubjectName { get; set; }
    public List<TeacherDto> Teachers { get; set; } = new();
    public DateTimeOffset ScheduledDate { get; set; }
    required public string SubjectColor { get; set; }
    public string? LearningGoals { get; set; }
    public string? Prerequisites { get; set; }
    public string? Description { get; set; }
	public SubjectStatus Status { get; set; }
}
