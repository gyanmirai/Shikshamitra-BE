using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Individual subject details in response
/// </summary>
public class SubjectAssignmentDetailResponseDto
{
	/// <summary>
	/// Assignment detail ID
	/// </summary>
	public Guid Id { get; set; }

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
	public string SubjectName { get; set; }

	/// <summary>
	/// Subject color (hex)
	/// </summary>
	public string SubjectColor { get; set; }

	/// <summary>
	/// Primary teacher ID (returned as string to match FE)
	/// </summary>
	public string? PrimaryTeacher { get; set; }

	/// <summary>
	/// Primary teacher name (fetched for convenience)
	/// </summary>
	public string? PrimaryTeacherName { get; set; }

	/// <summary>
	/// Secondary teacher IDs (returned as strings to match FE)
	/// </summary>
	public List<string>? SecondaryTeacher { get; set; }

	/// <summary>
	/// Secondary teacher details (names for display)
	/// </summary>
	public List<TeacherInfoDto>? SecondaryTeacherInfo { get; set; }

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
	public List<SubjectDocumentDto>? Documents { get; set; }

	/// <summary>
	/// Whether subject is open for enrollment
	/// </summary>
	public bool IsSubjectOpen { get; set; }
}
