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

	[StringLength(250, ErrorMessage = "Description cannot exceed 250 characters")]
	public string? Description { get; set; }

	public SubmissionType? DefaultSubmissionType { get; set; }

	[Range(0, double.MaxValue, ErrorMessage = "Default marks cannot be negative")]
	public decimal? DefaultMarks { get; set; }

	[Range(0, int.MaxValue, ErrorMessage = "Default due days cannot be negative")]
	public int? DefaultDueDays { get; set; }

	public bool EvaluationRequired { get; set; } = true;

	public EvaluationMethod? DefaultEvaluationMethod { get; set; }

	public TaskTypeStatus Status { get; set; } = TaskTypeStatus.Active;
}
