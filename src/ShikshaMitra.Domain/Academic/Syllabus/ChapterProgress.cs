using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Academic.Syllabus;

/// <summary>
/// Tracks the progress of a chapter for a specific division within a standard.
/// </summary>
public class ChapterProgress : FullAuditedEntity<Guid>
{
	/// <summary>
	/// Gets or sets the ID of the chapter being tracked.
	/// </summary>
	public Guid ChapterId { get; set; }

	/// <summary>
	/// Gets or sets the ID of the division (e.g., A, B, C).
	/// </summary>
	public Guid DivisionId { get; set; }

	/// <summary>
	/// Gets or sets the actual start date of the chapter in this division.
	/// </summary>
	public DateTime? StartDate { get; set; }

	/// <summary>
	/// Gets or sets the actual completion date of the chapter in this division.
	/// </summary>
	public DateTime? CompletionDate { get; set; }

	/// <summary>
	/// Gets or sets any additional notes or remarks about the progress.
	/// </summary>
	public string? Remarks { get; set; }

	/// <summary>
	/// Navigation property to the chapter.
	/// </summary>
	public SubjectChapter Chapter { get; set; }
}