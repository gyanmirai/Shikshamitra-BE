using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Filter DTO for getting assignment submissions
/// </summary>
public class GetAssignmentSubmissionsFilterDto
{
	[Required]
	public Guid ClassId { get; set; }

	[Required]
	public Guid DivisionId { get; set; }

	[Required]
	public Guid SubjectId { get; set; }

	[Required]
	public Guid AssignmentId { get; set; }
}
