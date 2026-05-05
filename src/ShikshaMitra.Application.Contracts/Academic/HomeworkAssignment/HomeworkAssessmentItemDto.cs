using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkAssessmentItemDto
{
    public Guid? Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? GrNumber { get; set; }
    public int? RollNo { get; set; }
    public SubmissionType SubmissionType { get; set; }
    public DateTimeOffset? SubmissionAt { get; set; }
    public DateTime DueAt { get; set; }
    public string? Feedback { get; set; }
    public SubmissionStatus Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public AttachmentInfoDto? Attachment { get; set; }
}
