using System;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO representing a single student's homework submission
/// </summary>
public class HomeworkSubmissionDto
{
	public string Id { get; set; } = string.Empty;
	public Guid StudentId { get; set; }
	public string RollNumber { get; set; } = string.Empty;
	public string GrNumber { get; set; } = string.Empty;
	public string StudentName { get; set; } = string.Empty;
	public string Homework { get; set; } = string.Empty;
	public string HomeworkType { get; set; } = string.Empty;
	public int SubmissionType { get; set; }
	public string? SubmissionDate { get; set; }
	public string DueDate { get; set; } = string.Empty;
	public decimal? Marks { get; set; }
	public decimal? TotalMarks { get; set; }
	public string? Feedback { get; set; }
	public string? Remarks { get; set; }
	public int Status { get; set; }
	public string SubmissionStatusLabel { get; set; } = string.Empty;
	public string EvaluationStatus { get; set; } = string.Empty;
	public string FeedbackStatus { get; set; } = string.Empty;
	public decimal? Percentage { get; set; }
	public string? SubmittedContent { get; set; }
	public string? SubmittedDocumentUrl { get; set; }
	public string? OriginalFileName { get; set; }
}
