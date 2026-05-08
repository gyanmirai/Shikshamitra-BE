using System.Collections.Generic;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Response DTO for assignment submissions list
/// </summary>
public class AssignmentSubmissionsResponseDto
{
	public string AssignmentId { get; set; } = string.Empty;
	public string AssignmentTitle { get; set; } = string.Empty;
	public string ClassId { get; set; } = string.Empty;
	public string DivisionId { get; set; } = string.Empty;
	public string SubjectId { get; set; } = string.Empty;
	public string DueDate { get; set; } = string.Empty;
	public List<HomeworkSubmissionDto> Data { get; set; } = new();
}
