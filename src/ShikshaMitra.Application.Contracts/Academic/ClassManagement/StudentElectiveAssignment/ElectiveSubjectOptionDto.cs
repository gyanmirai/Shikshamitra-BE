using System;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class ElectiveSubjectOptionDto
{
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; } = string.Empty;
	public string? SubjectColor { get; set; }
}
