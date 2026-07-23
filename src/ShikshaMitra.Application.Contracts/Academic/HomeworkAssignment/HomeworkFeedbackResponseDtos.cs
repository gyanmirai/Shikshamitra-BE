using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Response DTO for homework submissions list
/// </summary>
public class HomeworkSubmissionsResponseDto
{
	public string HomeworkId { get; set; } = string.Empty;
	public string HomeworkTitle { get; set; } = string.Empty;
	public string ClassId { get; set; } = string.Empty;
	public string DivisionId { get; set; } = string.Empty;
	public string SubjectId { get; set; } = string.Empty;
	public int SubmissionType { get; set; }
	public EvaluationWorkspaceSummaryDto Summary { get; set; } = new();
	public List<HomeworkSubmissionDto> Data { get; set; } = new();
}

public class EvaluationWorkspaceSummaryDto
{
	public int TotalStudents { get; set; }
	public int Submitted { get; set; }
	public int NotSubmitted { get; set; }
	public int Reviewed { get; set; }
	public int NeedsReview { get; set; }
	public int Late { get; set; }
	public decimal? TotalMarks { get; set; }
	public decimal? AverageMarks { get; set; }
}

/// <summary>
/// Response DTO for feedback operations
/// </summary>
public class FeedbackResponseDto
{
	public string Message { get; set; } = string.Empty;
	public FeedbackDataDto Data { get; set; } = null!;
}

/// <summary>
/// Feedback data DTO
/// </summary>
public class FeedbackDataDto
{
	public string SubmissionId { get; set; } = string.Empty;
	public string StudentId { get; set; } = string.Empty;
	public string HomeworkId { get; set; } = string.Empty;
	public decimal? Marks { get; set; }
	public decimal? TotalMarks { get; set; }
	public string? Feedback { get; set; }
	public string? Remarks { get; set; }
	public string Status { get; set; } = string.Empty;
}
