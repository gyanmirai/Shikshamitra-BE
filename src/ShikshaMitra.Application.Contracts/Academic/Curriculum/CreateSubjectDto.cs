using System;

namespace ShikshaMitra.Academic.Curriculum;

public class CreateSubjectDto
{
    public Guid ClassId { get; set; }
    public string? SubjectCode { get; set; }
    public string? SubjectName { get; set; }
    public Guid PrimaryTeacherId { get; set; }
    public DateTimeOffset ScheduledDate { get; set; }
    public string? SubjectColor { get; set; }
    public string? LearningGoals { get; set; }
    public string? Prerequisites { get; set; }
    public string? Description { get; set; }
}
