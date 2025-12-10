using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.ClassManagement.TeacherAssignment;

public class TeacherAssignmentUpdateDto
{
	[Required]
	public Guid DivisionId { get; set; }
	
	[Required]
	public Guid SubjectId { get; set; }
	
	[Required]
	public List<Guid> TeacherIds { get; set; } = new();
}
