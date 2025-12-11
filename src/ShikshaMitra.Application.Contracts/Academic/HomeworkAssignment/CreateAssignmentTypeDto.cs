using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for creating a new assignment type
/// </summary>
public class CreateAssignmentTypeDto
{
	/// <summary>
	/// Name of the assignment type
	/// </summary>
	[Required(ErrorMessage = "Assignment Type Name is required")]
	[StringLength(
		AssignmentTypeConsts.MaxNameLength,
		MinimumLength = AssignmentTypeConsts.MinNameLength,
		ErrorMessage = "Name must be between {2} and {1} characters"
	)]
	public string AssignmentTypeName { get; set; } = string.Empty;
}
