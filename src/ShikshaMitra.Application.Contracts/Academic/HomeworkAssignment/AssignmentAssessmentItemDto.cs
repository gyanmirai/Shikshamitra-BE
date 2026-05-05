using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class AssignmentAssessmentItemDto
{
    public Guid? Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? GrNumber { get; set; }
    public int? RollNo { get; set; }
    public SubmissionType SubmissionType { get; set; }
    public DateTimeOffset? SubmissionAt { get; set; }
    public DateTime DueAt { get; set; }
    public decimal TotalMarks { get; set; }
    public decimal? ObtainedMarks { get; set; }
    public decimal? Percentage { get; set; }
    public string? Grade { get; set; }
    public string? Feedback { get; set; }
    public SubmissionStatus Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public AttachmentInfoDto? Attachment { get; set; }
}
