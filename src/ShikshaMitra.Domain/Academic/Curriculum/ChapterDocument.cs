using System;
using ShikshaMitra.Academic.Syllabus;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Entity representing a document attached to a chapter
/// </summary>
public class ChapterDocument : Entity<Guid>
{
	/// <summary>
	/// The ID of the chapter this document belongs to
	/// </summary>
	public Guid ChapterId { get; set; }

	/// <summary>
	/// Navigation property to the parent chapter
	/// </summary>
	public virtual SubjectChapter Chapter { get; set; }

	/// <summary>
	/// Original file name
	/// </summary>
	public string FileName { get; set; }

	/// <summary>
	/// URL/path where the document is stored (blob storage, CDN, etc.)
	/// </summary>
	public string FileUrl { get; set; }

	/// <summary>
	/// File size in bytes
	/// </summary>
	public long FileSize { get; set; }

	/// <summary>
	/// MIME content type (e.g., application/pdf)
	/// </summary>
	public string ContentType { get; set; }

	/// <summary>
	/// When the document was uploaded
	/// </summary>
	public DateTime UploadedAt { get; set; }

	/// <summary>
	/// Private constructor for ORM
	/// </summary>
	private ChapterDocument()
	{
	}

	/// <summary>
	/// Constructor for creating a new chapter document
	/// </summary>
	public ChapterDocument(
		Guid id,
		Guid chapterId,
		string fileName,
		string fileUrl,
		long fileSize,
		string contentType
	)
		: base(id)
	{
		ChapterId = chapterId;
		FileName = fileName;
		FileUrl = fileUrl;
		FileSize = fileSize;
		ContentType = contentType;
		UploadedAt = DateTime.UtcNow;
	}
}
