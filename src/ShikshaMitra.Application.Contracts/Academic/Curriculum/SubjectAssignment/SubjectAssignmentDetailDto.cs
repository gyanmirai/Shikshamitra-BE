using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Individual subject details within an assignment group
/// </summary>
public class SubjectAssignmentDetailDto
{
	public Guid AssignedSubjectId { get; set; }

	/// <summary>
	/// Unique subject code (e.g., "SC101")
	/// </summary>
	[Required(ErrorMessage = "Subject Code is required")]
	[StringLength(
		SubjectAssignmentConsts.MaxSubjectCodeLength,
		MinimumLength = SubjectAssignmentConsts.MinSubjectCodeLength,
		ErrorMessage = "Subject code must be between {2} and {1} characters"
	)]
	public required string SubjectCode { get; set; }

	public SubjectType SubjectType { get; set; } = SubjectType.Core;

	public Guid? ElectiveGroupId { get; set; }

	/// <summary>
	/// Primary teacher ID (can be string "teacherId_123" from FE)
	/// </summary>
	public Guid PrimaryTeacher { get; set; }

	/// <summary>
	/// Secondary teacher IDs
	/// </summary>
	public List<Guid> SecondaryTeachers { get; set; } = new List<Guid>();

	/// <summary>
	/// Learning goals/objectives (HTML content)
	/// </summary>
	public string? LearningGoal { get; set; }

	/// <summary>
	/// Prerequisites for this subject (HTML content)
	/// </summary>
	public string? Prerequisites { get; set; }

	/// <summary>
	/// Full subject description (HTML content)
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Attached documents (syllabus, resources, etc.)
	/// </summary>
	public List<string> Documents { get; set; } = new List<string>();
}
