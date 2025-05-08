using System;

namespace ShikshaMitra.Academic.Curriculum.SubjectAssignment;

public class CreateStandardSubjectAssignmentDto
{
	public Guid StandardId { get; set; }
	public Guid SubjectId { get; set; }
	public string SubjectCode { get; set; } = default!;
	public string? LearningGoals { get; set; }
	public string? Prerequisites { get; set; }
	public string? Description { get; set; }
	public string? UploadedDocument { get; set; }
	public SubjectStatus Status { get; set; }
}
