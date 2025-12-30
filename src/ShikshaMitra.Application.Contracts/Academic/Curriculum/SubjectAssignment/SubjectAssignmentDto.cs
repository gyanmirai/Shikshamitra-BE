using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Response DTO for subject assignment group
/// Aligned with FE contract response structure
/// </summary>
public class SubjectAssignmentDto
{
	/// <summary>
	/// Assignment group ID
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Status: 0 = Draft, 1 = Published
	/// </summary>
	public SubjectStatus Status { get; set; }

	/// <summary>
	/// Subject type: 0 = Core, 1 = Elective
	/// </summary>
	public SubjectType SubjectType { get; set; }

	public Guid? ElectiveGroupId { get; set; }
	public string? ElectiveGroupName { get; set; }
	public string? Color { get; set; }

	/// <summary>
	/// List of subjects in this assignment group
	/// </summary>
	public List<SubjectAssignmentDetailResponseDto> Subjects { get; set; } = new();
}
