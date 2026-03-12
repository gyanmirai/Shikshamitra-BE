using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public class SaveClassTeacherAssignmentsDto
{
	[Required]
	public Guid OrganizationId { get; set; }

	[Required]
	[MinLength(1, ErrorMessage = "At least one assignment is required")]
	public List<ClassTeacherAssignmentUpdateDto> Assignments { get; set; } = new();
}
