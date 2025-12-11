using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.TeacherAssignment;

public class TeacherAssignmentGridResponseDto
{
	public string ClassName { get; set; }
	public List<DivisionRowDto> Divisions { get; set; } = new();
}
