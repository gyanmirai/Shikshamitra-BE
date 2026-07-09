using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for creating or updating a Subject
/// </summary>
public class CreateUpdateSubjectDto
{
	/// <summary>
	/// Subject name (e.g., Mathematics, Physics, etc.)
	/// </summary>
	[Required(ErrorMessage = "Subject name is required")]
	[StringLength(
		SubjectConsts.MaxNameLength,
		MinimumLength = SubjectConsts.MinNameLength,
		ErrorMessage = "Subject name must be between {2} and {1} characters"
	)]
	public required string Name { get; set; }

	[StringLength(SubjectConsts.MaxDisplayNameLength)]
	public string? DisplayName { get; set; }

	[StringLength(SubjectConsts.MaxCategoryLength)]
	public string? Category { get; set; }

	/// <summary>
	/// Hex color code for visual identification (e.g., #FF5733 or #F57)
	/// </summary>
	[Required(ErrorMessage = "Subject color is required")]
	[RegularExpression(
		SubjectConsts.ColorRegexPattern,
		ErrorMessage = SubjectConsts.ColorFormatErrorMessage
	)]
	public required string Color { get; set; }

	[StringLength(SubjectConsts.MaxIconLength)]
	public string? Icon { get; set; }

	[StringLength(SubjectConsts.MaxDescriptionLength)]
	public string? Description { get; set; }

	public SubjectMasterStatus Status { get; set; } = SubjectMasterStatus.Active;

	[Range(0, int.MaxValue, ErrorMessage = "Sort order cannot be negative")]
	public int? SortOrder { get; set; }

	/// <summary>
	/// ID of the Education Medium this subject belongs to
	/// </summary>
	[Required(ErrorMessage = "Education Medium ID is required")]
	public required Guid EducationMediumId { get; set; }
}
