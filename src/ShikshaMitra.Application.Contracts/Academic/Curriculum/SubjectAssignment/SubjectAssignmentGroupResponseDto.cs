using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Response DTO for subject assignment group
/// Aligned with FE contract response structure
/// </summary>
public class SubjectAssignmentGroupResponseDto
{
	/// <summary>
	/// Assignment group ID
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Standard/Class ID
	/// </summary>
	public Guid ClassId { get; set; }

	/// <summary>
	/// Assignment group name (e.g., "Science Group A")
	/// </summary>
	public string SubjectName { get; set; }

	/// <summary>
	/// Status: 0 = Draft, 1 = Published
	/// </summary>
	public int Status { get; set; }

	/// <summary>
	/// Subject type: 0 = Core, 1 = Elective
	/// </summary>
	public int SubjectType { get; set; }

	/// <summary>
	/// Academic medium ID
	/// </summary>
	public Guid AcademicMediumId { get; set; }

	/// <summary>
	/// List of subjects in this assignment group
	/// </summary>
	public List<SubjectAssignmentDetailResponseDto> Subjects { get; set; } = new();

	/// <summary>
	/// When this assignment was created
	/// </summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// When this assignment was published (null if Draft)
	/// </summary>
	public DateTime? PublishedAt { get; set; }
}
