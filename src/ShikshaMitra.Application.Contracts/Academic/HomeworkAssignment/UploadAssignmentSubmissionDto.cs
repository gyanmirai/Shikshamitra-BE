using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class UploadAssignmentSubmissionDto
{
    public Guid StudentId { get; set; }
    public Guid AssignmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? OriginalFileName { get; set; }
}
