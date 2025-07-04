using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Syllabus;

/// <summary>
/// Represents a chapter in a subject for a specific standard.
/// </summary>
public class SubjectChapter : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	#region Properties
	/// <summary>
	/// Gets or sets the tenant ID.
	/// </summary>
	public Guid? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the ID of the standard (e.g., Grade 1, Grade 2).
	/// </summary>
	public Guid StandardId { get; set; }

	/// <summary>
	/// Gets or sets the ID of the subject (e.g., Math, Science).
	/// </summary>
	public Guid SubjectId { get; set; }

	/// <summary>
	/// Gets or sets the chapter number (e.g., 1, 2, 3).
	/// </summary>
	public int ChapterNumber { get; set; }

	/// <summary>
	/// Gets or sets the name/title of the chapter.
	/// </summary>
	public string ChapterName { get; set; }

	/// <summary>
	/// Gets or sets the planned start time of the chapter.
	/// </summary>
	public DateTimeOffset ChapterStartTime { get; set; }

	/// <summary>
	/// Gets or sets the planned end time of the chapter.
	/// </summary>
	public DateTimeOffset ChapterEndTime { get; set; }

	/// <summary>
	/// Gets or sets an optional description of the chapter (e.g., objectives, summary).
	/// </summary>
	public string? Description { get; set; }

	#endregion

	#region Constructors

	// Default constructor
	protected SubjectChapter() { }

	// Parameterized constructor
	public SubjectChapter(
		Guid id,
		Guid standardId,
		Guid subjectId,
		int chapterNumber,
		string chapterName,
		DateTimeOffset chapterStartTime,
		DateTimeOffset chapterEndTime,
		string? description = null
	)
		: base(id)
	{
		StandardId = standardId;
		SubjectId = subjectId;
		ChapterNumber = chapterNumber;
		ChapterName = chapterName;
		ChapterStartTime = chapterStartTime;
		ChapterEndTime = chapterEndTime;
		Description = description;
	}

	#endregion

	#region Methods

	public void Update(
		string chapterName,
		DateTimeOffset chapterStartTime,
		DateTimeOffset chapterEndTime,
		string? description = null
	)
	{
		ChapterName = chapterName;
		ChapterStartTime = chapterStartTime;
		ChapterEndTime = chapterEndTime;
		Description = description;
	}
	#endregion
}
