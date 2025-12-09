using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for creating a new homework type
/// </summary>
public class CreateHomeworkTypeDto
{
	/// <summary>
	/// Name of the homework type
	/// </summary>
	[Required(ErrorMessage = "Homework Type Name is required")]
	[StringLength(
		HomeworkTypeConsts.MaxNameLength,
		MinimumLength = HomeworkTypeConsts.MinNameLength,
		ErrorMessage = "Name must be between {2} and {1} characters"
	)]
	public string HomeworkTypeName { get; set; } = string.Empty;
}
