using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class SubmissionUploadResponseDto
{
    public Guid SubmissionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ReferenceId { get; set; }
    public string? FileUrl { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public SubmissionStatus Status { get; set; }
}
