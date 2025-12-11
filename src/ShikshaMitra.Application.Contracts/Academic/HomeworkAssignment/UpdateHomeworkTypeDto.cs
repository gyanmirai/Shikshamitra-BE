using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for updating an existing homework type
/// </summary>
public class UpdateHomeworkTypeDto
{
	/// <summary>
	/// Updated name of the homework type
	/// </summary>
	[Required(ErrorMessage = "Homework Type Name is required")]
	[StringLength(
		HomeworkTypeConsts.MaxNameLength,
		MinimumLength = HomeworkTypeConsts.MinNameLength,
		ErrorMessage = "Name must be between {2} and {1} characters"
	)]
	public string HomeworkTypeName { get; set; } = string.Empty;
}
