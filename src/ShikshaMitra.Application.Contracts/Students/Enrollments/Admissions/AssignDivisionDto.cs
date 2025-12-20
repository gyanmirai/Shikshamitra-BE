using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Enrollments.Admissions;

/// <summary>
/// DTO for assigning division to a student
/// </summary>
public class AssignDivisionDto
{
	[Required]
	public Guid DivisionId { get; set; }
}
