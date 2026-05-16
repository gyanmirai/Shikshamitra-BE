using System;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

public class SubjectForChaptersDto
{
    public Guid SubjectId { get; set; }
    public Guid SubjectAssignmentId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
}
