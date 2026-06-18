using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class LockStudentElectiveAssignmentsDto
{
	[Required]
	public Guid AcademicMediumId { get; set; }

	[Required]
	public Guid ClassId { get; set; }

	[Required]
	public Guid DivisionId { get; set; }

	[Required]
	public Guid ElectiveGroupId { get; set; }
}
