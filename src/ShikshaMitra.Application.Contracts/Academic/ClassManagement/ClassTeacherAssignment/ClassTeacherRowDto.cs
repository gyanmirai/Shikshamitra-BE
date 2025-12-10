using System;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public class ClassTeacherRowDto
{
	public Guid ClassId { get; set; }
	public string ClassName { get; set; }
	public Guid DivisionId { get; set; }
	public string DivisionName { get; set; }
	public AssignedTeacherDto? AssignedTeacher { get; set; }
}

public class AssignedTeacherDto
{
	public Guid TeacherId { get; set; }
	public string TeacherName { get; set; }
}
