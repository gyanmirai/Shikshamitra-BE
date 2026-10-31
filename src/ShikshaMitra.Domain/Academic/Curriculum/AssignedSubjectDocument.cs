using System;
using ShikshaMitra.Academic.Syllabus;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Entity representing a document attached to a chapter
/// </summary>
public class AssignedSubjectDocument : FullAuditedEntity<Guid>, IMultiTenant
{
	/// <summary>
	/// Gets or sets the tenant ID.
	/// </summary>
	public Guid? TenantId { get; set; }

	public Guid AssignedSubjectId { get; set; }

	/// <summary>
	/// The ID of the chapter this document belongs to
	/// </summary>
	public Guid? ChapterId { get; set; }

	/// <summary>
	/// Original file name
	/// </summary>
	public string BlobName { get; set; }

	/// <summary>
	/// When the document was uploaded
	/// </summary>
	public DateTimeOffset UploadedAt { get; set; }

	/// <summary>
	/// Private constructor for ORM
	/// </summary>
	private AssignedSubjectDocument()
	{
		// EF materializes these required columns after this ORM constructor.
		BlobName = null!;
	}

	/// <summary>
	/// Constructor for creating a new chapter document
	/// </summary>
	public AssignedSubjectDocument(
		Guid id,
		Guid assignedSubjectId,
		Guid? chapterId,
		string blobName
	)
		: base(id)
	{
		AssignedSubjectId = assignedSubjectId;
		BlobName = blobName;
		ChapterId = chapterId;
		UploadedAt = DateTime.UtcNow;
	}
}
