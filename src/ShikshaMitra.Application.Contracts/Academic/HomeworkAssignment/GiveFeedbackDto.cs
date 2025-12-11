using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for giving or updating feedback on a student's homework submission
/// </summary>
public class GiveFeedbackDto
{
	/// <summary>
	/// Marks obtained by the student
	/// </summary>
	public decimal? Marks { get; set; }

	/// <summary>
	/// Total marks for the homework
	/// </summary>
	public decimal? TotalMarks { get; set; }

	/// <summary>
	/// Feedback text/message from teacher
	/// </summary>
	[StringLength(2000)]
	public string? Feedback { get; set; }

	/// <summary>
	/// Additional remarks
	/// </summary>
	[StringLength(1000)]
	public string? Remarks { get; set; }

	/// <summary>
	/// Submission status (4 = Reviewed, 6 = Needs Review, etc.)
	/// </summary>
	[Required]
	public SubmissionStatus Status { get; set; }
}
