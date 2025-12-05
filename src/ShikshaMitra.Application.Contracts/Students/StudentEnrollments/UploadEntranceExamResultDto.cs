using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// DTO for uploading entrance exam result
/// </summary>
public class UploadEntranceExamResultDto
{
	[Required]
	[Range(1, 1000, ErrorMessage = "Total marks must be between 1 and 1000")]
	public decimal TotalMarks { get; set; }

	[Required]
	[Range(0, 1000, ErrorMessage = "Passing marks must be between 0 and 1000")]
	public decimal PassingMarks { get; set; }

	[Required]
	[Range(0, 1000, ErrorMessage = "Obtained marks must be between 0 and 1000")]
	public decimal ObtainedMarks { get; set; }
}
