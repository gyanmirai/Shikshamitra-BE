using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Individual subject details in response
/// </summary>
public class SubjectAssignmentDetailResponseDto
{
	/// <summary>
	/// Assignment detail ID
	/// </summary>
	public Guid AssignedId { get; set; }

	/// <summary>
	/// Subject code (e.g., "SC101")
	/// </summary>
	public string SubjectCode { get; set; }

	/// <summary>
	/// Subject ID from master
	/// </summary>
	public Guid? SubjectId { get; set; }

	/// <summary>
	/// Subject name (e.g., "Physics")
	/// </summary>
	public SubjectDto Subject { get; set; }

	public Guid? ElectiveGroupId { get; set; }

	/// <summary>
	/// Primary teacher ID (can be string "teacherId_123" from FE)
	/// </summary>
	public Guid PrimaryTeacher { get; set; }

	/// <summary>
	/// Secondary teacher IDs
	/// </summary>
	public List<Guid> SecondaryTeachers { get; set; } = new List<Guid>();

	/// <summary>
	/// Learning goals (HTML)
	/// </summary>
	public string? LearningGoal { get; set; }

	/// <summary>
	/// Prerequisites (HTML)
	/// </summary>
	public string? Prerequisites { get; set; }

	/// <summary>
	/// Description (HTML)
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Document attachments
	/// </summary>
	public List<string>? Documents { get; set; }
}
