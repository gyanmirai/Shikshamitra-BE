using System;
using ShikshaMitra.Students.Documents;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

/// <summary>
/// Represents a document uploaded for a student during registration process.
/// </summary>
public class StudentDocument : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid DocumentTypeId { get; private set; }
	public string BlobName { get; private set; }
	public string FileName { get; private set; }
	public string ContentType { get; private set; }
	public long Size { get; private set; }
	public string Sha256Hash { get; private set; } = string.Empty;
	public StudentDocumentStorageStatus StorageStatus { get; private set; } = StudentDocumentStorageStatus.Stored;
	public Guid? UploadedByUserId { get; private set; }
	public DateTimeOffset? ValidatedAt { get; private set; }
	public int DownloadCount { get; private set; }
	public DateTimeOffset? LastDownloadedAt { get; private set; }
	public bool IsSensitive { get; private set; }

	private StudentDocument()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StudentDocument(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid documentTypeId,
		string blobName,
		string fileName,
		string contentType,
		long size,
		string? sha256Hash = null,
		Guid? uploadedByUserId = null,
		DateTimeOffset? validatedAt = null,
		bool isSensitive = false)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		DocumentTypeId = documentTypeId;
		SetDocumentMetadata(blobName, fileName, contentType, size, sha256Hash, uploadedByUserId, validatedAt, isSensitive);
	}

	public void UpdateDocument(
		string blobName,
		string fileName,
		string contentType,
		long size,
		string? sha256Hash = null,
		Guid? uploadedByUserId = null,
		DateTimeOffset? validatedAt = null,
		bool? isSensitive = null)
	{
		SetDocumentMetadata(blobName, fileName, contentType, size, sha256Hash, uploadedByUserId, validatedAt, isSensitive ?? IsSensitive);
	}

	public void MarkDownloaded(DateTimeOffset downloadedAt)
	{
		DownloadCount++;
		LastDownloadedAt = downloadedAt;
	}

	public void MarkDeleted()
	{
		StorageStatus = StudentDocumentStorageStatus.Deleted;
	}

	private void SetDocumentMetadata(
		string blobName,
		string fileName,
		string contentType,
		long size,
		string? sha256Hash,
		Guid? uploadedByUserId,
		DateTimeOffset? validatedAt,
		bool isSensitive)
	{
		BlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName));
		FileName = Check.NotNullOrWhiteSpace(fileName, nameof(fileName));
		ContentType = Check.NotNullOrWhiteSpace(contentType, nameof(contentType));

		if (size <= 0)
		{
			throw new ArgumentException("File size must be greater than zero", nameof(size));
		}

		Size = size;
		Sha256Hash = string.IsNullOrWhiteSpace(sha256Hash)
			? Sha256Hash
			: Check.Length(sha256Hash, nameof(sha256Hash), 128);
		UploadedByUserId = uploadedByUserId ?? UploadedByUserId;
		ValidatedAt = validatedAt ?? ValidatedAt;
		IsSensitive = isSensitive;
		StorageStatus = StudentDocumentStorageStatus.Stored;
	}
}
