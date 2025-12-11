using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.TeacherAssignment;

public class DivisionRowDto
{
	public Guid DivisionId { get; set; }
	public string DivisionName { get; set; }
	public List<DivisionSubjectAssignmentDto> Subjects { get; set; } = new();
}
