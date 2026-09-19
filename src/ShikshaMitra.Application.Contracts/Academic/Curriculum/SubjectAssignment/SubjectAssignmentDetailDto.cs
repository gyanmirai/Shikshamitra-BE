using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Individual subject details within an assignment group
/// </summary>
public class SubjectAssignmentDetailDto
{
	public Guid SubjectId { get; set; }

	/// <summary>
	/// Unique subject code (e.g., "SC101")
	/// </summary>
	public string? SubjectCode { get; set; }

	public Guid? ElectiveGroupId { get; set; }

	/// <summary>
	/// All teachers assigned to the subject. The order has no user-facing role or priority.
	/// When supplied, this replaces the legacy primary/secondary input fields.
	/// </summary>
	public List<Guid>? TeacherIds { get; set; }

	/// <summary>
	/// Legacy compatibility field. New clients should use <see cref="TeacherIds"/>.
	/// </summary>
	public Guid? PrimaryTeacherId { get; set; }

	/// <summary>
	/// Legacy compatibility field. New clients should use <see cref="TeacherIds"/>.
	/// </summary>
	public List<Guid>? SecondaryTeacherIds { get; set; } = new List<Guid>();

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
	public List<string>? Documents { get; set; } = new List<string>();

	/// <summary>
	/// Number of periods per week
	/// </summary>
	public int WeeklyPeriods { get; set; } = 0;

	/// <summary>
	/// Difficulty level (0=Beginner, 1=Intermediate, 2=Advanced)
	/// </summary>
	public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Beginner;

	/// <summary>
	/// Subject group or department (e.g., "Science", "Humanities")
	/// </summary>
	public string? SubjectGroup { get; set; }

	/// <summary>
	/// Whether this subject was suggested by AI
	/// </summary>
	public bool IsAiSuggested { get; set; } = false;

	/// <summary>
	/// Whether this subject was copied from previous year
	/// </summary>
	public bool IsCopiedFromPreviousYear { get; set; } = false;
}
