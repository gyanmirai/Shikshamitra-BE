using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for creating a subject assignment group (Draft or Published)
/// Aligned with FE contract: POST /api/subject-assignments?academicMediumId={id}
/// </summary>
public class CreateUpdateSubjectAssignmentDto
{
	/// <summary>
	/// Standard/Class ID
	/// </summary>
	[Required(ErrorMessage = "classId is required")]
	public Guid ClassId { get; set; }

	/// <summary>
	/// Assignment status: 0 = Draft (Craft), 1 = Published
	/// </summary>
	[Required]
	[Range(typeof(SubjectStatus), "0", "1", ErrorMessage = "Status must be 0 (Draft) or 1 (Published)")]
	public SubjectStatus Status { get; set; }

	/// <summary>
	/// List of subjects in this assignment group (minimum 1 required)
	/// </summary>
	[Required(ErrorMessage = "At least one subject is required")]
	[MinLength(1, ErrorMessage = "subjects array must contain at least 1 subject")]
	public List<SubjectAssignmentDetailDto> Subjects { get; set; } = new();
}
