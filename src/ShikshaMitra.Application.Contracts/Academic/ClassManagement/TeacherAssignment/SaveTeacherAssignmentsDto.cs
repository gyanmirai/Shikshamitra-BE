using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.ClassManagement.TeacherAssignment;

public class SaveTeacherAssignmentsDto
{
	[Required]
	public Guid AcademicMediumId { get; set; }
	
	[Required]
	public Guid ClassId { get; set; }
	
	[Required]
	[MinLength(1, ErrorMessage = "At least one assignment is required")]
	public List<TeacherAssignmentUpdateDto> Assignments { get; set; } = new();
}
