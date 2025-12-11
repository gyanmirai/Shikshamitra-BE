using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class StudentElectiveUpdateDto
{
	[Required]
	public Guid StudentId { get; set; }

	[Required]
	public Guid SelectedSubjectId { get; set; }
}
