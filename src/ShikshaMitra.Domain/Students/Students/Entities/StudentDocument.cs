using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

/// <summary>
/// Represents a document uploaded for a student during registration process
/// </summary>
public class StudentDocument : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }

	/// <summary>
	/// Reference to the DocumentType configuration
	/// </summary>
	public Guid DocumentTypeId { get; private set; }

	/// <summary>
	/// Blob storage path/key for the document
	/// </summary>
	public string BlobName { get; private set; }

	/// <summary>
	/// Original filename uploaded by user
	/// </summary>
	public string FileName { get; private set; }

	/// <summary>
	/// MIME type of the document
	/// </summary>
	public string ContentType { get; private set; }

	/// <summary>
	/// File size in bytes
	/// </summary>
	public long Size { get; private set; }

	/// <summary>
	/// ORM constructor
	/// </summary>
	private StudentDocument()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	/// <summary>
	/// Constructor for creating a new student document
	/// </summary>
	public StudentDocument(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid documentTypeId,
		string blobName,
		string fileName,
		string contentType,
		long size)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		DocumentTypeId = documentTypeId;
		BlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName));
		FileName = Check.NotNullOrWhiteSpace(fileName, nameof(fileName));
		ContentType = Check.NotNullOrWhiteSpace(contentType, nameof(contentType));

		if (size <= 0)
		{
			throw new ArgumentException("File size must be greater than zero", nameof(size));
		}
		Size = size;
	}

	/// <summary>
	/// Updates the document with new content
	/// </summary>
	public void UpdateDocument(
		string blobName,
		string fileName,
		string contentType,
		long size)
	{
		BlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName));
		FileName = Check.NotNullOrWhiteSpace(fileName, nameof(fileName));
		ContentType = Check.NotNullOrWhiteSpace(contentType, nameof(contentType));

		if (size <= 0)
		{
			throw new ArgumentException("File size must be greater than zero", nameof(size));
		}
		Size = size;
	}
}
