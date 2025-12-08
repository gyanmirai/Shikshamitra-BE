using System;
using System.Collections.Generic;
using ShikshaMitra.Academic.Curriculum;
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

	/// <summary>
	/// Gets or sets the education medium ID for filtering/validation
	/// </summary>
	public Guid EducationMediumId { get; set; }

	/// <summary>
	/// Navigation property for chapter documents
	/// </summary>
	public virtual ICollection<ChapterDocument> Documents { get; set; }

	#endregion

	#region Constructors

	// Default constructor
	protected SubjectChapter() { }

	// Parameterized constructor
	public SubjectChapter(
		Guid id,
		Guid standardId,
		Guid subjectId,
		Guid educationMediumId,
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
		EducationMediumId = educationMediumId;
		ChapterNumber = chapterNumber;
		ChapterName = chapterName;
		ChapterStartTime = chapterStartTime;
		ChapterEndTime = chapterEndTime;
		Description = description;
		Documents = new List<ChapterDocument>();
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

	/// <summary>
	/// Updates the chapter dates
	/// </summary>
	public void UpdateDates(DateTimeOffset startTime, DateTimeOffset endTime)
	{
		ChapterStartTime = startTime;
		ChapterEndTime = endTime;
	}

	/// <summary>
	/// Updates chapter details
	/// </summary>
	public void UpdateDetails(int chapterNumber, string chapterName, string? description)
	{
		ChapterNumber = chapterNumber;
		ChapterName = chapterName;
		Description = description;
	}

	/// <summary>
	/// Adds a document to the chapter
	/// </summary>
	public void AddDocument(ChapterDocument document)
	{
		Documents ??= new List<ChapterDocument>();
		Documents.Add(document);
	}

	/// <summary>
	/// Removes a document from the chapter
	/// </summary>
	public void RemoveDocument(ChapterDocument document)
	{
		Documents?.Remove(document);
	}
	#endregion
}
