using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.TeacherAssignment;

public class DivisionSubjectAssignmentDto
{
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }
	public string SubjectCode { get; set; }
	public List<TeacherInfoDto> AssignedTeachers { get; set; } = new();
}
