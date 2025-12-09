using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for creating or updating an elective group
/// </summary>
public class CreateUpdateElectiveGroupDto
{
	/// <summary>
	/// Name of the elective group
	/// </summary>
	[Required(ErrorMessage = "Elective group name is required")]
	[StringLength(
		ElectiveGroupConsts.MaxNameLength,
		MinimumLength = ElectiveGroupConsts.MinNameLength,
		ErrorMessage = "Group name must be between {2} and {1} characters"
	)]
	public required string Name { get; set; }

	/// <summary>
	/// Hex color code for UI display (e.g., #FF5722 or #F57)
	/// </summary>
	[Required(ErrorMessage = "Color is required")]
	[RegularExpression(
		SubjectConsts.ColorRegexPattern,
		ErrorMessage = "Color must be a valid hex code (e.g., #FF5722 or #F57)"
	)]
	public required string Color { get; set; }

	/// <summary>
	/// List of subject IDs to include in this elective group (minimum 2)
	/// </summary>
	[Required(ErrorMessage = "Subject selection is required")]
	[MinLength(
		ElectiveGroupConsts.MinSubjectsRequired,
		ErrorMessage = "At least {1} subjects must be selected for an elective group"
	)]
	public required List<Guid> SubjectIds { get; set; }

	// Note: EducationMediumId comes from route parameter, not request body
	// This prevents mismatch between route and body
}
