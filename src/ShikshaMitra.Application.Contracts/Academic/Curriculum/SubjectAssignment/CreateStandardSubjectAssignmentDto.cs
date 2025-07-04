using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum.SubjectAssignment;

public class CreateStandardSubjectAssignmentDto
{
	public Guid StandardId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid? ElectiveGroupId { get; set; }
	public required string SubjectCode { get; set; }
	public Guid? PrimaryTeacherId { get; set; }
	public List<Guid> SecondaryTeacherIds { get; set; } = new List<Guid>();
	public string? LearningGoals { get; set; }
	public string? Prerequisites { get; set; }
	public string? Description { get; set; }
	public string? UploadedDocument { get; set; }
	public SubjectStatus Status { get; set; }
}
