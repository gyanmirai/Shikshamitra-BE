using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public class ClassTeacherAssignmentUpdateDto
{
	[Required]
	public Guid ClassId { get; set; }

	[Required]
	public Guid DivisionId { get; set; }

	[Required]
	public Guid TeacherId { get; set; }

	public string? AssignmentType { get; set; }

	public DateTime? EffectiveFrom { get; set; }

	public DateTime? EffectiveTo { get; set; }

	public string? ChangeReason { get; set; }

	public string? Notes { get; set; }
}
