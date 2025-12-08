using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Individual subject details within an assignment group
/// </summary>
public class SubjectAssignmentDetailDto
{
	/// <summary>
	/// Unique subject code (e.g., "SC101")
	/// </summary>
	[Required(ErrorMessage = "Subject Code is required")]
	[StringLength(
		SubjectAssignmentConsts.MaxSubjectCodeLength,
		MinimumLength = SubjectAssignmentConsts.MinSubjectCodeLength,
		ErrorMessage = "Subject code must be between {2} and {1} characters"
	)]
	public string SubjectCode { get; set; }

	/// <summary>
	/// Subject name (e.g., "Physics") - from FE
	/// </summary>
	[Required(ErrorMessage = "Subject name is required")]
	public string SubjectName { get; set; }

	/// <summary>
	/// Subject display color (hex format, e.g., "#00AABB")
	/// </summary>
	[Required(ErrorMessage = "Subject color is required")]
	[RegularExpression(
		SubjectConsts.ColorRegexPattern,
		ErrorMessage = "Subject color must be a valid hex code"
	)]
	public string SubjectColor { get; set; }

	/// <summary>
	/// Primary teacher ID (can be string "teacherId_123" from FE)
	/// </summary>
	public string? PrimaryTeacher { get; set; }

	/// <summary>
	/// Secondary teacher IDs
	/// </summary>
	public List<string>? SecondaryTeacher { get; set; }

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
	public List<SubjectDocumentDto>? Documents { get; set; }

	/// <summary>
	/// Whether this subject is open for enrollment
	/// </summary>
	public bool IsSubjectOpen { get; set; } = true;
}
