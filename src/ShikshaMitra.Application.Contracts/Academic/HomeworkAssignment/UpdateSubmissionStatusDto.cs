using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for updating submission status only
/// </summary>
public class UpdateSubmissionStatusDto
{
	/// <summary>
	/// New submission status
	/// </summary>
	[Required]
	public SubmissionStatus Status { get; set; }
}
