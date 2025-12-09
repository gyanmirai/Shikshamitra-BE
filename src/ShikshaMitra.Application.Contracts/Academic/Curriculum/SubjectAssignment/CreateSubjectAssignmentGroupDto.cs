using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for creating a subject assignment group (Draft or Published)
/// Aligned with FE contract: POST /api/subject-assignments?academicMediumId={id}
/// </summary>
public class CreateSubjectAssignmentGroupDto
{
	/// <summary>
	/// Standard/Class ID
	/// </summary>
	[Required(ErrorMessage = "classId is required")]
	public Guid ClassId { get; set; }

	/// <summary>
	/// Assignment group name (e.g., "Science Group A")
	/// Top-level title for the subject group
	/// </summary>
	[Required(ErrorMessage = "Subject is required")]
	[StringLength(
		SubjectAssignmentConsts.MaxGroupNameLength,
		MinimumLength = SubjectAssignmentConsts.MinGroupNameLength,
		ErrorMessage = "Subject name must be between {2} and {1} characters"
	)]
	public string SubjectName { get; set; }

	/// <summary>
	/// Assignment status: 0 = Draft (Craft), 1 = Published
	/// </summary>
	[Required]
	[Range(0, 1, ErrorMessage = "Status must be 0 (Draft) or 1 (Published)")]
	public int Status { get; set; }

	/// <summary>
	/// Subject type: 0 = Core, 1 = Elective
	/// </summary>
	[Required]
	[Range(0, 1, ErrorMessage = "SubjectType must be 0 (Core) or 1 (Elective)")]
	public int SubjectType { get; set; }

	/// <summary>
	/// List of subjects in this assignment group (minimum 1 required)
	/// </summary>
	[Required(ErrorMessage = "At least one subject is required")]
	[MinLength(1, ErrorMessage = "subjects array must contain at least 1 subject")]
	public List<SubjectAssignmentDetailDto> Subjects { get; set; } = new();
}
