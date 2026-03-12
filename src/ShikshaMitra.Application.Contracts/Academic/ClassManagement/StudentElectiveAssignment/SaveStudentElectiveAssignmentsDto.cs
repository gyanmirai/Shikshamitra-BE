using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class SaveStudentElectiveAssignmentsDto
{
	[Required]
	public Guid AcademicMediumId { get; set; }

	[Required]
	public Guid ElectiveGroupId { get; set; }

	[Required]
	[MinLength(1, ErrorMessage = "At least one assignment is required")]
	public List<StudentElectiveUpdateDto> Assignments { get; set; } = new();
}
