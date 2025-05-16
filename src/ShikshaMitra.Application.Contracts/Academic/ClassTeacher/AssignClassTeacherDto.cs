using System;

namespace ShikshaMitra.Academic.ClassTeacher;

public class AssignClassTeacherDto
{
	public Guid TeacherId { get; set; }
	public Guid ClassId { get; set; }
	public Guid DivisionId { get; set; }
}
