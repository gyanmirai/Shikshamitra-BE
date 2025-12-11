using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.ClassManagement.TeacherAssignment;

public class TeacherInfoDto
{
	public Guid TeacherId { get; set; }
	public string TeacherName { get; set; } = string.Empty;
}
