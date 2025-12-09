using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Filter DTO for getting homework submissions
/// </summary>
public class GetHomeworkSubmissionsFilterDto
{
	[Required]
	public Guid ClassId { get; set; }

	[Required]
	public Guid DivisionId { get; set; }

	[Required]
	public Guid SubjectId { get; set; }

	[Required]
	public Guid HomeworkId { get; set; }
}
