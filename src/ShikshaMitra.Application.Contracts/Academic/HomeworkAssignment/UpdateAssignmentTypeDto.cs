using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for updating an existing assignment type
/// </summary>
public class UpdateAssignmentTypeDto
{
	/// <summary>
	/// Updated name of the assignment type
	/// </summary>
	[Required(ErrorMessage = "Assignment Type Name is required")]
	[StringLength(
		AssignmentTypeConsts.MaxNameLength,
		MinimumLength = AssignmentTypeConsts.MinNameLength,
		ErrorMessage = "Name must be between {2} and {1} characters"
	)]
	public string AssignmentTypeName { get; set; } = string.Empty;

	[StringLength(250, ErrorMessage = "Description cannot exceed 250 characters")]
	public string? Description { get; set; }

	public SubmissionType? DefaultSubmissionType { get; set; }

	[Range(0, double.MaxValue, ErrorMessage = "Default marks cannot be negative")]
	public decimal? DefaultMarks { get; set; }

	[Range(1, int.MaxValue, ErrorMessage = "Default due days must be at least one")]
	public int? DefaultDueDays { get; set; }

	public bool EvaluationRequired { get; set; } = true;

	public EvaluationMethod? DefaultEvaluationMethod { get; set; }

	public TaskTypeStatus Status { get; set; } = TaskTypeStatus.Active;
}
