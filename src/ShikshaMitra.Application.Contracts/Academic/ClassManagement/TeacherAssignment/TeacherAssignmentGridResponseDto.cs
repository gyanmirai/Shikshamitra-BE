using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.TeacherAssignment;

public class TeacherAssignmentGridResponseDto
{
	public string ClassName { get; set; } = null!; // The response mapper populates these members after construction.
	public List<DivisionRowDto> Divisions { get; set; } = new();
}
