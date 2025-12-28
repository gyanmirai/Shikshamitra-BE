using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Nested subject details within a subject assignment group response
/// </summary>
public class NestedSubjectAssignmentDto
{
	/// <summary>
	/// Assigned subject ID
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Subject ID
	/// </summary>
	public Guid SubjectId { get; set; }

	/// <summary>
	/// Subject name
	/// </summary>
	public string SubjectName { get; set; } = string.Empty;

	/// <summary>
	/// Subject code (e.g., "SC101")
	/// </summary>
	public string? SubjectCode { get; set; }

	/// <summary>
	/// Subject color
	/// </summary>
	public string? Color { get; set; }

	/// <summary>
	/// Primary teacher ID
	/// </summary>
	public Guid? PrimaryTeacherId { get; set; }

	/// <summary>
	/// Secondary teacher IDs
	/// </summary>
	public List<Guid>? SecondaryTeacherId { get; set; } = new();

	/// <summary>
	/// Learning goals/objectives (HTML content)
	/// </summary>
	public string? LearningGoal { get; set; }

	/// <summary>
	/// Prerequisites for this subject (HTML content)
	/// </summary>
	public string? Prerequisites { get; set; }

	/// <summary>
	/// Full subject description (HTML content)
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Attached documents (syllabus, resources, etc.)
	/// </summary>
	public List<string>? Documents { get; set; } = new();
}
