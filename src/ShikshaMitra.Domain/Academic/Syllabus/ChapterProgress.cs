using ShikshaMitra.Academic.Curriculum;
using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Academic.Syllabus;

/// <summary>
/// Tracks the progress of a chapter for a specific division within a standard.
/// </summary>
public class ChapterProgress : FullAuditedEntity<Guid>
{
	#region Properties

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
	/// Gets or sets the progress percentage (0-100).
	/// </summary>
	public int Progress { get; set; }

	/// <summary>
	/// Gets or sets any additional notes or remarks about the progress.
	/// </summary>
	public string? Remarks { get; set; }

	/// <summary>
	/// Navigation property to the chapter.
	/// </summary>
	public AssignedSubjectChapter Chapter { get; set; }

	#endregion

	#region Constructors
	private ChapterProgress() { } // Required for EF Core

	public ChapterProgress(Guid chapterId, Guid divisionId)
		: this(Guid.NewGuid(), chapterId, divisionId)
	{
	}

	public ChapterProgress(Guid id, Guid chapterId, Guid divisionId)
		: base(id)
	{
		ChapterId = chapterId;
		DivisionId = divisionId;
	}

	#endregion

	#region Methods

	public void UpdateProgress(int progress, string? description = null)
	{
		if (progress < 0 || progress > 100)
		{
			throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100.");
		}

		Progress = progress;

		if (!string.IsNullOrWhiteSpace(description))
		{
			Remarks = description;
		}

		if (progress == 100 && !CompletionDate.HasValue)
		{
			CompletionDate = DateTime.UtcNow;
		}
		else if (progress < 100)
		{
			CompletionDate = null;
		}

		if (progress > 0 && !StartDate.HasValue)
		{
			StartDate = DateTime.UtcNow;
		}
	}

	#endregion
}
