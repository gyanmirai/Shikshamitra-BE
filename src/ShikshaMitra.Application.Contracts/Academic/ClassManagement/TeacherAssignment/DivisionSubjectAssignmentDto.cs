using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.TeacherAssignment;

public class DivisionSubjectAssignmentDto
{
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; } = null!; // The response mapper populates these members after construction.
	public string SubjectCode { get; set; } = null!;
	public List<TeacherInfoDto> AssignedTeachers { get; set; } = new();
}
